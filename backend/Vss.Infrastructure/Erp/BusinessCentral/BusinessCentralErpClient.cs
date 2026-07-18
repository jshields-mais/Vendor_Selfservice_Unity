using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Vss.Infrastructure.Erp.BusinessCentral;

/// <summary>
/// <see cref="IErpClient"/> over the Dynamics 365 Business Central OData v2.0 API
/// (`…/companies({id})/vendors`). OAuth2 bearer per request; writes use ETag If-Match.
/// </summary>
public class BusinessCentralErpClient(
    HttpClient http,
    BusinessCentralOptions options,
    IBcTokenProvider tokens,
    ILogger<BusinessCentralErpClient> logger) : IErpClient
{
    private string CompanyBase => $"{options.BaseUrl.TrimEnd('/')}/companies({options.CompanyId})";

    public async Task<ErpVendorDto?> GetVendorAsync(string vendorNumber, CancellationToken ct = default)
    {
        var el = await FirstVendorAsync($"number eq '{Esc(vendorNumber)}'", ct);
        return el is null ? null : MapVendor(el.Value);
    }

    public async Task<ErpVendorDto?> MatchVendorAsync(MatchQuery query, CancellationToken ct = default)
    {
        // Tax ID + ZIP is the portable match. Vendor# + PIN → match by number; the PIN
        // is an app-issued value with no BC equivalent and is verified app-side.
        if (query.Method == "TaxIdZip" && !string.IsNullOrWhiteSpace(query.TaxId))
        {
            var arr = await QueryVendorsAsync($"taxRegistrationNumber eq '{Esc(query.TaxId!)}'", ct);
            foreach (var e in arr)
            {
                var dto = MapVendor(e);
                if (string.IsNullOrWhiteSpace(query.Zip) || Norm(dto.RemitZip) == Norm(query.Zip))
                    return dto;
            }
            return null;
        }
        return await GetVendorAsync(query.VendorNumber ?? "", ct);
    }

    public async Task UpdateVendorMasterAsync(string vendorNumber, VendorMasterPatch patch, CancellationToken ct = default)
    {
        var el = await FirstVendorAsync($"number eq '{Esc(vendorNumber)}'", ct)
                 ?? throw new InvalidOperationException($"BC vendor {vendorNumber} not found.");
        var id = el.GetProperty("id").GetString();
        var etag = el.TryGetProperty("@odata.etag", out var e) ? e.GetString() : "*";

        var body = BuildPatchBody(patch);
        using var req = await AuthRequestAsync(HttpMethod.Patch, $"{CompanyBase}/vendors({id})", ct);
        req.Headers.TryAddWithoutValidation("If-Match", etag ?? "*");
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"BC PATCH vendor {vendorNumber} failed: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync(ct)}");

        logger.LogInformation("[BC] PATCH vendors({Id}) — {Fields}", id, string.Join(", ", patch.Fields.Keys));
    }

    // ---------------------------------------------------------------- http
    private async Task<HttpRequestMessage> AuthRequestAsync(HttpMethod method, string url, CancellationToken ct)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(ct));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return req;
    }

    private async Task<IReadOnlyList<JsonElement>> QueryVendorsAsync(string filter, CancellationToken ct)
    {
        var url = $"{CompanyBase}/vendors?$filter={Uri.EscapeDataString(filter)}";
        using var resp = await http.SendAsync(await AuthRequestAsync(HttpMethod.Get, url, ct), ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("value").EnumerateArray().Select(x => x.Clone()).ToList();
    }

    private async Task<JsonElement?> FirstVendorAsync(string filter, CancellationToken ct)
    {
        var list = await QueryVendorsAsync(filter, ct);
        return list.Count == 0 ? null : list[0];
    }

    // ---------------------------------------------------------------- mapping
    private static readonly Dictionary<string, string> TopFieldMap = new()
    {
        ["LegalName"] = "displayName",
        ["PrimaryPhone"] = "phoneNumber",
        ["PrimaryEmail"] = "email",
        ["Tin"] = "taxRegistrationNumber",
    };
    private static readonly Dictionary<string, string> AddressFieldMap = new()
    {
        ["RemitStreet"] = "street",
        ["RemitCity"] = "city",
        ["RemitState"] = "state",
        ["RemitZip"] = "postalCode",
        ["RemitCountry"] = "countryRegionCode",
    };

    internal static string BuildPatchBody(VendorMasterPatch patch)
    {
        var top = new Dictionary<string, object?>();
        var address = new Dictionary<string, object?>();
        foreach (var (field, value) in patch.Fields)
        {
            if (TopFieldMap.TryGetValue(field, out var top1)) top[top1] = value;
            else if (AddressFieldMap.TryGetValue(field, out var addr))
                address[addr] = field == "RemitCountry" ? CountryCode(value) : value;
            // Banking (BankName/RoutingNumber/AccountNumber/...) is not on the standard
            // vendor entity — it lives under vendorBankAccounts. [TODO: second call.]
        }
        if (address.Count > 0) top["address"] = address;
        return JsonSerializer.Serialize(top);
    }

    private static ErpVendorDto MapVendor(JsonElement v)
    {
        string? S(string n) => v.TryGetProperty(n, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
        var addr = v.TryGetProperty("address", out var a) && a.ValueKind == JsonValueKind.Object ? a : default;
        string? A(string n) => addr.ValueKind == JsonValueKind.Object && addr.TryGetProperty(n, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

        var blocked = S("blocked");
        return new ErpVendorDto
        {
            Number = S("number") ?? "",
            LegalName = S("displayName") ?? "",
            Status = string.IsNullOrWhiteSpace(blocked) || blocked == " " ? "Active" : $"Blocked ({blocked})",
            RemitStreet = A("street") ?? "",
            RemitCity = A("city") ?? "",
            RemitState = A("state") ?? "",
            RemitZip = A("postalCode") ?? "",
            RemitCountry = A("countryRegionCode") ?? "",
            PrimaryPhone = S("phoneNumber"),
            PrimaryEmail = S("email"),
            Tin = S("taxRegistrationNumber"),
        };
    }

    private static string Esc(string s) => s.Replace("'", "''");
    private static string Norm(string? s) => (s ?? "").Trim().ToUpperInvariant();
    private static string CountryCode(string? c) => c switch { "United States" => "US", "Canada" => "CA", _ => c ?? "" };
}
