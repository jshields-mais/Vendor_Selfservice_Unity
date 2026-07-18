using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Vss.Infrastructure.Erp.SapByDesign;

/// <summary>
/// <see cref="IErpClient"/> over SAP Business ByDesign SOAP A2X services:
/// <c>QuerySupplierIn</c> (FindByElements) for read/match and <c>ManageSupplierIn</c>
/// (MaintainBundle_V1) for writes, HTTP Basic auth.
///
/// The SOAP element namespaces and exact field paths are tenant/WSDL-specific. They are
/// centralised in <see cref="Sap"/> and the envelope builders below, and responses are
/// parsed by local element name (namespace-agnostic) so this is resilient to those
/// specifics. [TODO: confirm the message shapes against the sandbox WSDL/sample payloads.]
/// </summary>
public class SapByDesignErpClient : IErpClient
{
    private readonly HttpClient _http;
    private readonly SapByDesignOptions _opt;
    private readonly ILogger<SapByDesignErpClient> _log;

    public SapByDesignErpClient(HttpClient http, SapByDesignOptions options, ILogger<SapByDesignErpClient> log)
    {
        _http = http;
        _opt = options;
        _log = log;
        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{options.Username}:{options.Password}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
    }

    public async Task<ErpVendorDto?> GetVendorAsync(string vendorNumber, CancellationToken ct = default)
        => ParseSupplier(await PostAsync(_opt.QuerySupplierPath, Sap.QueryAction,
            Sap.BuildQueryByInternalId(vendorNumber), ct));

    public async Task<ErpVendorDto?> MatchVendorAsync(MatchQuery query, CancellationToken ct = default)
    {
        // QuerySupplierIn selects by supplier number (InternalID); it has no tax-id
        // selection, so Tax ID + ZIP linking isn't supported here — match by number.
        if (string.IsNullOrWhiteSpace(query.VendorNumber)) return null;
        return ParseSupplier(await PostAsync(_opt.QuerySupplierPath, Sap.QueryAction,
            Sap.BuildQueryByInternalId(query.VendorNumber!), ct));
    }

    private static readonly string[] AddressFields =
        { "RemitCountry", "RemitStreet", "RemitCity", "RemitState", "RemitZip", "PrimaryEmail", "PrimaryPhone" };
    private static readonly string[] BankingFields = { "RoutingNumber", "AccountNumber" };

    public async Task UpdateVendorMasterAsync(string vendorNumber, VendorMasterPatch patch, CancellationToken ct = default)
    {
        var fields = new Dictionary<string, string?>(patch.Fields);
        var ctx = new SapMaintainContext();
        var needsAddress = AddressFields.Any(fields.ContainsKey);
        var needsBanking = BankingFields.Any(fields.ContainsKey);

        // Address + bank changes update existing records in place, which needs their keys
        // (AddressInformation UUID / BankDetails ID + routing) — read the supplier first.
        if (needsAddress || needsBanking)
        {
            var q = await PostAsync(_opt.QuerySupplierPath, Sap.QueryAction, Sap.BuildQueryByInternalId(vendorNumber), ct);

            if (needsAddress)
            {
                ctx.AddressUuid = q.Descendants().FirstOrDefault(e => e.Name.LocalName == "AddressInformation")
                    ?.Elements().FirstOrDefault(e => e.Name.LocalName == "UUID")?.Value;

                var current = ParseSupplier(q);
                void Fill(string key, string? value) { if (!fields.ContainsKey(key) && !string.IsNullOrEmpty(value)) fields[key] = value; }
                Fill("RemitCountry", current?.RemitCountry);
                Fill("RemitStreet", current?.RemitStreet);
                Fill("RemitCity", current?.RemitCity);
                Fill("RemitState", current?.RemitState);
                Fill("RemitZip", current?.RemitZip);
            }

            if (needsBanking)
                ctx.Bank = ResolveBankMaintain(q, fields, patch.EffectiveDate);
        }

        var doc = await PostAsync(_opt.ManageSupplierPath, Sap.ManageAction,
            Sap.BuildMaintainBundle(vendorNumber, fields, ctx), ct);

        // MaximumLogItemSeverityCode "3" = error.
        if (Local(doc.Root, "MaximumLogItemSeverityCode") == "3")
            throw new InvalidOperationException(
                $"SAP ByDesign ManageSupplier for {vendorNumber} failed: {Local(doc.Root, "Note") ?? "unknown error"}");

        _log.LogInformation("[SAP ByD] MaintainBundle supplier {Number} — {Fields}", vendorNumber, string.Join(", ", patch.Fields.Keys));
    }

    /// <summary>
    /// Decides how to write bank details given the supplier's current bank record(s) and
    /// the approval date, implementing SAP's validity-dated bank data:
    /// <list type="bullet">
    /// <item>No existing record → create one, valid from approval date to unlimited.</item>
    /// <item>Account changed (A → B) → end-date the current record (valid-to = day before
    ///   approval) and add a new record valid from approval date to unlimited.</item>
    /// <item>Same account (e.g. routing correction) → update in place, validity untouched.</item>
    /// </list>
    /// </summary>
    internal static SapBankMaintain ResolveBankMaintain(
        XDocument query, IReadOnlyDictionary<string, string?> fields, DateTimeOffset? effectiveDate)
    {
        var validFrom = DateOnly.FromDateTime((effectiveDate ?? DateTimeOffset.UtcNow).UtcDateTime);

        var banks = query.Descendants().Where(e => e.Name.LocalName == "BankDetails").ToList();
        var existing = banks.FirstOrDefault();
        string? E(string n) => existing?.Elements().FirstOrDefault(e => e.Name.LocalName == n)?.Value;

        var existingId = E("ID");
        var existingRouting = E("BankRoutingID");
        var existingType = E("BankRoutingIDTypeCode");
        var existingAccount = E("BankAccountID");

        var newAccount = fields.GetValueOrDefault("AccountNumber");
        var newRouting = fields.GetValueOrDefault("RoutingNumber") ?? existingRouting;

        // No bank on file yet → create the first record, dated from approval to unlimited.
        if (string.IsNullOrEmpty(existingId))
            return new SapBankMaintain
            {
                Id = "0001", ActionCode = "01",
                RoutingId = newRouting, RoutingIdTypeCode = existingType,
                AccountId = newAccount ?? existingAccount,
                WriteValidity = true, ValidFrom = validFrom, ValidTo = Sap.UnlimitedDate,
            };

        // A genuine account switch (A → B): end-date the current record and add a new one.
        var accountChanged = !string.IsNullOrEmpty(newAccount)
            && !string.IsNullOrEmpty(existingAccount)
            && !AccountsEqual(newAccount, existingAccount);
        if (accountChanged)
        {
            var existingStart = ParseSapDate(existing?.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "ValidityPeriod")
                ?.Elements().FirstOrDefault(e => e.Name.LocalName == "StartDate")?.Value) ?? validFrom;

            return new SapBankMaintain
            {
                Id = NextBankId(banks), ActionCode = "01",
                RoutingId = newRouting, RoutingIdTypeCode = existingType,
                AccountId = newAccount,
                WriteValidity = true, ValidFrom = validFrom, ValidTo = Sap.UnlimitedDate,
                Prior = new SapBankPriorClose
                {
                    Id = existingId!, RoutingId = existingRouting, RoutingIdTypeCode = existingType,
                    AccountId = existingAccount,
                    // Prior account stays valid until the day before the new one takes effect.
                    ValidFrom = existingStart, ValidTo = validFrom.AddDays(-1),
                },
            };
        }

        // Same account (routing/other correction) → update in place, validity unchanged.
        return new SapBankMaintain
        {
            Id = existingId!, ActionCode = "04",
            RoutingId = newRouting, RoutingIdTypeCode = existingType,
            AccountId = newAccount,
            WriteValidity = false,
        };
    }

    private static bool AccountsEqual(string a, string b) =>
        a.Trim().TrimStart('0') == b.Trim().TrimStart('0');

    /// <summary>Next sequential BankDetails ID ("0001" → "0002"), zero-padded to 4 digits.</summary>
    private static string NextBankId(IEnumerable<XElement> banks)
    {
        var max = banks
            .Select(b => b.Elements().FirstOrDefault(e => e.Name.LocalName == "ID")?.Value)
            .Select(id => int.TryParse(id, out var n) ? n : 0)
            .DefaultIfEmpty(0).Max();
        return (max + 1).ToString("D4");
    }

    private static DateOnly? ParseSapDate(string? s) =>
        DateOnly.TryParse(s, out var d) ? d : null;

    // ---------------------------------------------------------------- http
    private async Task<XDocument> PostAsync(string path, string soapAction, string envelope, CancellationToken ct)
    {
        var url = $"{_opt.BaseUrl.TrimEnd('/')}{path}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(envelope, Encoding.UTF8, "text/xml"),
        };
        req.Headers.TryAddWithoutValidation("SOAPAction", $"\"{soapAction}\"");

        using var resp = await _http.SendAsync(req, ct);
        var xml = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"SAP ByDesign {path} failed: {(int)resp.StatusCode} {Truncate(xml)}");
        return XDocument.Parse(xml);
    }

    // ---------------------------------------------------------------- parsing
    internal static ErpVendorDto? ParseSupplier(XDocument doc)
    {
        var supplier = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Supplier");
        if (supplier is null) return null;

        string? F(string name) => Local(supplier, name);
        return new ErpVendorDto
        {
            Number = F("InternalID") ?? "",
            LegalName = F("FirstLineName") ?? F("FormattedName") ?? F("BusinessPartnerFormattedName") ?? "",
            Status = F("LifeCycleStatusCode") ?? "Active",
            RemitStreet = F("StreetName") ?? "",
            RemitCity = F("CityName") ?? "",
            RemitState = F("RegionCode") ?? "",
            RemitZip = F("StreetPostalCode") ?? F("PostalCode") ?? "",
            RemitCountry = F("CountryCode") ?? "",
            PrimaryPhone = F("FormattedNumberDescription") ?? F("TelephoneNumber"),
            PrimaryEmail = F("EmailURI") ?? F("URI"),
            Tin = F("TaxID") ?? F("PartyTaxID"),
        };
    }

    /// <summary>First descendant value with the given local element name (namespace-agnostic).</summary>
    private static string? Local(XElement? root, string localName)
        => root?.Descendants().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

    private static string Truncate(string s) => s.Length > 500 ? s[..500] : s;
}
