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
        var envelope = query.Method == "TaxIdZip" && !string.IsNullOrWhiteSpace(query.TaxId)
            ? Sap.BuildQueryByTaxId(query.TaxId!)
            : Sap.BuildQueryByInternalId(query.VendorNumber ?? "");

        var dto = ParseSupplier(await PostAsync(_opt.QuerySupplierPath, Sap.QueryAction, envelope, ct));
        if (dto is null) return null;
        if (query.Method == "TaxIdZip" && !string.IsNullOrWhiteSpace(query.Zip) && Norm(dto.RemitZip) != Norm(query.Zip))
            return null;
        return dto;
    }

    public async Task UpdateVendorMasterAsync(string vendorNumber, VendorMasterPatch patch, CancellationToken ct = default)
    {
        var doc = await PostAsync(_opt.ManageSupplierPath, Sap.ManageAction,
            Sap.BuildMaintainBundle(vendorNumber, patch.Fields), ct);

        // MaximumLogItemSeverityCode "3" = error.
        var severity = Local(doc.Root, "MaximumLogItemSeverityCode");
        if (severity == "3")
        {
            var note = Local(doc.Root, "Note") ?? "unknown error";
            throw new InvalidOperationException($"SAP ByDesign ManageSupplier for {vendorNumber} failed: {note}");
        }
        _log.LogInformation("[SAP ByD] MaintainBundle supplier {Number} — {Fields}", vendorNumber, string.Join(", ", patch.Fields.Keys));
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

    private static string Norm(string? s) => (s ?? "").Trim().ToUpperInvariant();
    private static string Truncate(string s) => s.Length > 500 ? s[..500] : s;
}
