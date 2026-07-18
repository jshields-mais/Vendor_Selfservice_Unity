using Microsoft.Extensions.Logging;
using Vss.Domain;

namespace Vss.Infrastructure.Erp;

/// <summary>
/// In-memory stand-in for the City's ERP. Serves the seeded vendor master, matches
/// linking credentials, and "pushes" approved changes by mutating its own copy and
/// logging — so the approval workflow is fully observable without a real ERP.
/// </summary>
public class StubErpClient : IErpClient
{
    private readonly ILogger<StubErpClient> _logger;
    private readonly Dictionary<string, ErpVendorDto> _vendors;
    private readonly Dictionary<string, (string Pin, string? Tin, string Zip)> _matchCreds;

    public StubErpClient(ILogger<StubErpClient> logger)
    {
        _logger = logger;
        var seed = SeedData.Vendors();
        _vendors = seed.ToDictionary(v => v.Number, ToDto);
        _matchCreds = seed.ToDictionary(v => v.Number, v => (v.Pin, v.Tin, v.RemitZip));
    }

    public Task<ErpVendorDto?> GetVendorAsync(string vendorNumber, CancellationToken ct = default)
        => Task.FromResult(_vendors.TryGetValue(vendorNumber, out var v) ? v : null);

    public Task<ErpVendorDto?> MatchVendorAsync(MatchQuery query, CancellationToken ct = default)
    {
        foreach (var (number, creds) in _matchCreds)
        {
            var hit = query.Method == "TaxIdZip"
                ? Norm(query.TaxId) == Norm(creds.Tin) && Norm(query.Zip) == Norm(creds.Zip)
                : Norm(query.VendorNumber) == Norm(number) && Norm(query.Pin) == Norm(creds.Pin);

            if (hit)
            {
                _logger.LogInformation("[ERP stub] matched vendor {Number} via {Method}", number, query.Method);
                return Task.FromResult<ErpVendorDto?>(_vendors[number]);
            }
        }
        _logger.LogInformation("[ERP stub] no match for {Method}", query.Method);
        return Task.FromResult<ErpVendorDto?>(null);
    }

    public Task UpdateVendorMasterAsync(string vendorNumber, VendorMasterPatch patch, CancellationToken ct = default)
    {
        if (!_vendors.TryGetValue(vendorNumber, out var dto))
        {
            _logger.LogWarning("[ERP stub] PUT for unknown vendor {Number}", vendorNumber);
            return Task.CompletedTask;
        }

        foreach (var (field, value) in patch.Fields)
        {
            var prop = typeof(ErpVendorDto).GetProperty(field);
            if (prop is not null && prop.PropertyType == typeof(string))
                prop.SetValue(dto, value);
            _logger.LogInformation("[ERP stub] PUT /vendors/{Number}/master — {Field} = {Value}", vendorNumber, field, value);
        }
        return Task.CompletedTask;
    }

    public Task<bool> AddSupplierAttachmentAsync(string vendorNumber, ErpAttachment att, CancellationToken ct = default)
    {
        _logger.LogInformation("[ERP stub] attachment {File} ({Bytes} bytes) added to vendor {Number}",
            att.FileName, att.Content.Length, vendorNumber);
        return Task.FromResult(true);
    }

    private static string Norm(string? s) => (s ?? string.Empty).Trim().ToUpperInvariant();

    private static ErpVendorDto ToDto(Vendor v) => new()
    {
        Number = v.Number,
        LegalName = v.LegalName,
        Dba = v.Dba,
        EntityType = v.EntityType,
        Website = v.Website,
        Status = v.Status,
        RemitStreet = v.RemitStreet,
        RemitCity = v.RemitCity,
        RemitState = v.RemitState,
        RemitZip = v.RemitZip,
        RemitCountry = v.RemitCountry,
        PaymentMethod = v.PaymentMethod,
        BankName = v.BankName,
        RoutingNumber = v.RoutingNumber,
        AccountNumber = v.AccountNumber,
        AccountType = v.AccountType,
        TaxIdType = v.TaxIdType,
        Tin = v.Tin,
        TaxClassification = v.TaxClassification,
        PrimaryContact = v.PrimaryContact,
        PrimaryEmail = v.PrimaryEmail,
        PrimaryPhone = v.PrimaryPhone,
        Category = v.CategoryCodes.FirstOrDefault()?.Code ?? string.Empty,
    };
}
