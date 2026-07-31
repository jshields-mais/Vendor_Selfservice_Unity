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
        { "RemitCountry", "RemitStreet", "HouseNumber", "RemitCity", "RemitState", "RemitZip",
          "AddressType", "PoBox", "PrimaryEmail", "PrimaryPhone" };
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

                // Resolve PO Box vs street: an explicit AddressType change wins, else keep the
                // supplier's current shape. Normalise to IsPoBox for the envelope builder.
                var isPoBox = fields.TryGetValue("AddressType", out var at) && at is not null
                    ? string.Equals(at, "PO Box", StringComparison.OrdinalIgnoreCase)
                    : (current?.IsPoBox ?? false);
                fields["IsPoBox"] = isPoBox ? "true" : "false";

                Fill("RemitCountry", current?.RemitCountry);
                Fill("RemitCity", current?.RemitCity);
                Fill("RemitState", current?.RemitState);
                if (isPoBox)
                {
                    Fill("PoBox", current?.PoBox);
                    Fill("RemitZip", current?.RemitZip); // POBoxPostalCode (parsed into RemitZip)
                }
                else
                {
                    Fill("RemitStreet", current?.RemitStreet);
                    Fill("HouseNumber", current?.HouseNumber);
                    Fill("RemitZip", current?.RemitZip);
                }
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

    public async Task<bool> AddSupplierAttachmentAsync(string vendorNumber, ErpAttachment att, CancellationToken ct = default)
    {
        var base64 = Convert.ToBase64String(att.Content);
        var doc = await PostAsync(_opt.ManageSupplierPath, Sap.ManageAction,
            Sap.BuildAddAttachment(vendorNumber, att.FileName, att.MimeType, base64), ct);

        if (Local(doc.Root, "MaximumLogItemSeverityCode") == "3")
        {
            var note = Local(doc.Root, "Note") ?? "unknown error";
            // A same-named attachment already on the supplier is not a real failure — the file
            // is already there, so treat it as an idempotent success rather than blocking approval.
            if (note.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                _log.LogInformation("[SAP ByD] attachment {File} already on supplier {Number}; treated as attached", att.FileName, vendorNumber);
                return true;
            }
            throw new InvalidOperationException($"SAP ByDesign attachment for {vendorNumber} failed: {note}");
        }

        _log.LogInformation("[SAP ByD] attachment {File} ({Bytes} bytes) added to supplier {Number}",
            att.FileName, att.Content.Length, vendorNumber);
        return true;
    }

    public async Task<int> UpdateCommunicationPreferencesAsync(string vendorNumber, IReadOnlyList<ErpCommunicationPreference> prefs, CancellationToken ct = default)
    {
        // Resolve each preference to its SAP codes; skip documents/channels without a code.
        var arrangements = prefs
            .Select(p => (svc: string.IsNullOrEmpty(p.ServiceInterfaceCode)
                                   ? CommunicationCatalog.ServiceInterfaceCodeFor(p.BusinessDocument)
                                   : p.ServiceInterfaceCode,
                          med: CommunicationCatalog.MediumCodeFor(p.Channel), p.Email, p.Enabled))
            .Where(x => x.svc is not null && x.med is not null)
            .Select(x => (x.svc!, x.med!, x.Email, x.Enabled))
            .ToList();
        if (arrangements.Count == 0) return 0;

        var doc = await PostAsync(_opt.ManageSupplierPath, Sap.ManageAction,
            Sap.BuildCommunicationArrangements(vendorNumber, arrangements), ct);
        if (Local(doc.Root, "MaximumLogItemSeverityCode") == "3")
            throw new InvalidOperationException(
                $"SAP ByDesign communication preferences for {vendorNumber} failed: {Local(doc.Root, "Note") ?? "unknown error"}");

        _log.LogInformation("[SAP ByD] {Count} communication arrangement(s) set on supplier {Number}", arrangements.Count, vendorNumber);
        return arrangements.Count;
    }

    private static SapContact ToSapContact(ErpContact c) => new()
    {
        Uuid = c.SapUuid,
        InternalId = c.SapInternalId,
        FirstName = c.FirstName,
        LastName = c.LastName,
        FormOfAddressCode = c.Title,
        FunctionCode = c.Function,
        DepartmentCode = c.Department,
        Email = c.Email,
        Phone = c.Phone,
        Mobile = c.Mobile,
        Fax = c.Fax,
    };

    public async Task<ErpContactResult> UpsertContactAsync(string vendorNumber, ErpContact contact, CancellationToken ct = default)
    {
        // Create (no keys) vs update in place (has SapUuid). "01" = create, "04" = save/update.
        var creating = string.IsNullOrEmpty(contact.SapUuid);
        var actionCode = creating ? "01" : "04";
        var doc = await PostAsync(_opt.ManageSupplierPath, Sap.ManageAction,
            Sap.BuildContactPerson(vendorNumber, ToSapContact(contact), actionCode), ct);
        if (Local(doc.Root, "MaximumLogItemSeverityCode") == "3")
            throw new InvalidOperationException(
                $"SAP ByDesign contact {(creating ? "create" : "update")} for {vendorNumber} failed: {Local(doc.Root, "Note") ?? "unknown error"}");

        // Prefer the keys echoed in the confirmation; for a create, fall back to re-reading the
        // supplier and matching the new contact by email/name so we can persist its SAP keys.
        var respCp = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ContactPerson");
        string? uuid = respCp?.Elements().FirstOrDefault(e => e.Name.LocalName == "BusinessPartnerContactUUID")?.Value ?? contact.SapUuid;
        string? internalId = respCp?.Elements().FirstOrDefault(e => e.Name.LocalName == "BusinessPartnerContactInternalID")?.Value ?? contact.SapInternalId;

        if (creating && string.IsNullOrEmpty(uuid))
        {
            var re = await GetVendorAsync(vendorNumber, ct);
            var match = re?.Contacts.FirstOrDefault(x =>
                (!string.IsNullOrEmpty(contact.Email) && string.Equals(x.Email, contact.Email, StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(x.FirstName, contact.FirstName, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(x.LastName, contact.LastName, StringComparison.OrdinalIgnoreCase)));
            uuid = match?.SapUuid; internalId = match?.SapInternalId;
        }

        _log.LogInformation("[SAP ByD] ContactPerson {Action} on supplier {Number} (uuid {Uuid})",
            creating ? "created" : "updated", vendorNumber, uuid ?? "?");
        return new ErpContactResult(uuid, internalId);
    }

    public async Task DeleteContactAsync(string vendorNumber, string? sapUuid, string? sapInternalId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(sapUuid) && string.IsNullOrEmpty(sapInternalId))
            return; // never synced to SAP — nothing to delete there
        var contact = new SapContact { Uuid = sapUuid, InternalId = sapInternalId };
        var doc = await PostAsync(_opt.ManageSupplierPath, Sap.ManageAction,
            Sap.BuildContactPerson(vendorNumber, contact, "03"), ct);
        if (Local(doc.Root, "MaximumLogItemSeverityCode") == "3")
            throw new InvalidOperationException(
                $"SAP ByDesign contact delete for {vendorNumber} failed: {Local(doc.Root, "Note") ?? "unknown error"}");
        _log.LogInformation("[SAP ByD] ContactPerson deleted on supplier {Number} (uuid {Uuid})", vendorNumber, sapUuid ?? sapInternalId);
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

    /// <summary>SAP stores names uppercase (JOE HARDESTY); present them as "Joe Hardesty".</summary>
    private static string TitleCase(string s) => string.Join(" ",
        s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
         .Select(w => w.Length == 1 ? w.ToUpperInvariant() : char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));

    private static string? TitleCaseOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : TitleCase(s);

    /// <summary>The default ContactPerson element (or the first), if any.</summary>
    internal static XElement? FindDefaultContact(XElement supplier)
    {
        var contacts = supplier.Descendants().Where(e => e.Name.LocalName == "ContactPerson").ToList();
        return contacts.FirstOrDefault(c => string.Equals(
                   c.Elements().FirstOrDefault(e => e.Name.LocalName == "DefaultContactPersonIndicator")?.Value, "true", StringComparison.OrdinalIgnoreCase))
               ?? contacts.FirstOrDefault();
    }

    /// <summary>Maps one SAP ContactPerson element to an <see cref="ErpContact"/>.</summary>
    internal static ErpContact ParseContact(XElement contact)
    {
        string? C(string n) => contact.Elements().FirstOrDefault(e => e.Name.LocalName == n)?.Value;
        var c = new ErpContact
        {
            SapUuid = C("BusinessPartnerContactUUID"),
            SapInternalId = C("BusinessPartnerContactInternalID"),
            IsPrimary = string.Equals(C("DefaultContactPersonIndicator"), "true", StringComparison.OrdinalIgnoreCase),
            FirstName = TitleCaseOrNull(C("GivenName")),
            LastName = TitleCaseOrNull(C("FamilyName")),
            Title = C("FormOfAddressCode"),
            Function = C("BusinessPartnerFunctionTypeCode"),
            Department = C("BusinessPartnerFunctionalAreaCode"),
            Email = C("WorkplaceEMailURI"),
            Fax = C("WorkplaceFacsimileFormattedNumberDescription"),
        };
        foreach (var tel in contact.Elements().Where(e => e.Name.LocalName == "WorkplaceTelephone"))
        {
            var num = tel.Elements().FirstOrDefault(e => e.Name.LocalName == "FormattedNumberDescription")?.Value;
            var isMobile = string.Equals(tel.Elements().FirstOrDefault(e => e.Name.LocalName == "MobilePhoneNumberIndicator")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(num)) continue;
            if (isMobile) c.Mobile = num; else c.Phone = num;
        }
        return c;
    }

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
        var dto = new ErpVendorDto
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

        // All ContactPersons on the supplier → the vendor's contact list. Names are
        // title-cased (SAP stores upper); Title/Function/Department are the SAP codes.
        dto.Contacts = supplier.Descendants().Where(e => e.Name.LocalName == "ContactPerson")
            .Select(ParseContact).ToList();

        // PO Box vs street address. SAP flags a PO Box with POBoxIndicator=true and carries
        // the box number/postal code in dedicated fields (no StreetName). Surface the right
        // shape so the portal shows PO Box fields for a PO Box and street fields otherwise.
        if (string.Equals(F("POBoxIndicator"), "true", StringComparison.OrdinalIgnoreCase))
        {
            dto.IsPoBox = true;
            dto.PoBox = F("POBoxID");
            dto.RemitStreet = "";
            dto.RemitZip = F("POBoxPostalCode") ?? dto.RemitZip;
        }
        else
        {
            dto.HouseNumber = F("HouseID");
        }

        // Payment method (PaymentData/PaymentForm/PaymentFormCode) drives whether the
        // portal shows bank fields, so surface it as the mapped display value.
        var payCode = F("PaymentFormCode");
        if (!string.IsNullOrEmpty(payCode)) dto.PaymentMethod = MapPaymentForm(payCode);

        // Active bank record = the one whose validity period covers today (SAP keeps
        // superseded accounts as end-dated records). Surface its routing/account so the
        // portal loads the current bank data.
        var bank = SelectActiveBank(supplier);
        if (bank is not null)
        {
            string? B(string n) => bank.Elements().FirstOrDefault(e => e.Name.LocalName == n)?.Value;
            dto.RoutingNumber = B("BankRoutingID") ?? dto.RoutingNumber;
            dto.AccountNumber = B("BankAccountID") ?? dto.AccountNumber;
            var acctType = B("BankAccountTypeCode");
            if (!string.IsNullOrEmpty(acctType)) dto.AccountType = MapAccountType(acctType);
        }
        return dto;
    }

    /// <summary>SAP PaymentFormCode → portal payment-method label. 06 = cheque, 05 = bank
    /// transfer; anything else electronic falls back to ACH/EFT so bank fields still show.</summary>
    private static string MapPaymentForm(string code) => code switch
    {
        "06" => "Check",
        "05" => "ACH / EFT",
        _ => "ACH / EFT",
    };

    /// <summary>SAP BankAccountTypeCode → portal account type (03 = current/checking).</summary>
    private static string MapAccountType(string code) => code switch
    {
        "02" => "Savings",
        _ => "Checking",
    };

    /// <summary>Picks the bank record whose validity period contains today; falls back to
    /// the first record when none carry a validity period.</summary>
    private static XElement? SelectActiveBank(XElement supplier)
    {
        var banks = supplier.Descendants().Where(e => e.Name.LocalName == "BankDetails").ToList();
        if (banks.Count == 0) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly? Bound(XElement b, string el) => ParseSapDate(b.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "ValidityPeriod")
            ?.Elements().FirstOrDefault(e => e.Name.LocalName == el)?.Value);

        var active = banks.FirstOrDefault(b =>
        {
            var start = Bound(b, "StartDate");
            var end = Bound(b, "EndDate");
            return (start is null || start <= today) && (end is null || end >= today);
        });
        return active ?? banks[0];
    }

    /// <summary>First descendant value with the given local element name (namespace-agnostic).</summary>
    private static string? Local(XElement? root, string localName)
        => root?.Descendants().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

    private static string Truncate(string s) => s.Length > 500 ? s[..500] : s;
}
