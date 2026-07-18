namespace Vss.Infrastructure.Erp;

/// <summary>
/// The vendor master as the ERP exposes it. Deliberately a flat DTO — the ERP is
/// an external system, so we don't leak domain entities across the boundary.
/// </summary>
public class ErpVendorDto
{
    public string Number { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string? Dba { get; set; }
    public string EntityType { get; set; } = "LLC";
    public string? Website { get; set; }
    public string Status { get; set; } = "Active";

    public string RemitStreet { get; set; } = string.Empty;
    public string RemitCity { get; set; } = string.Empty;
    public string RemitState { get; set; } = string.Empty;
    public string RemitZip { get; set; } = string.Empty;
    public string RemitCountry { get; set; } = "United States";

    public string PaymentMethod { get; set; } = "ACH / EFT";
    public string? BankName { get; set; }
    public string? RoutingNumber { get; set; }
    public string? AccountNumber { get; set; }
    public string AccountType { get; set; } = "Checking";

    public string TaxIdType { get; set; } = "EIN";
    public string? Tin { get; set; }
    public string? TaxClassification { get; set; }

    public string? PrimaryContact { get; set; }
    public string? PrimaryEmail { get; set; }
    public string? PrimaryPhone { get; set; }

    public string Category { get; set; } = string.Empty;
}

/// <summary>Credentials a portal user submits to match an existing ERP vendor.</summary>
public class MatchQuery
{
    public string Method { get; set; } = "VendorNumberPin"; // or "TaxIdZip"
    public string? VendorNumber { get; set; }
    public string? Pin { get; set; }
    public string? TaxId { get; set; }
    public string? Zip { get; set; }
}

/// <summary>A set of approved field changes to apply to the ERP vendor master.</summary>
public class VendorMasterPatch
{
    /// <summary>Field name → new value, as reviewed/approved by City staff.</summary>
    public Dictionary<string, string?> Fields { get; set; } = new();
}

/// <summary>
/// Boundary to the City's ERP vendor master. The portal never writes the ERP
/// directly outside this interface; approvals call <see cref="UpdateVendorMasterAsync"/>.
/// Swap <c>StubErpClient</c> for a real <c>UnityErpClient</c> (IntegrationService v2) later.
/// </summary>
public interface IErpClient
{
    Task<ErpVendorDto?> GetVendorAsync(string vendorNumber, CancellationToken ct = default);
    Task<ErpVendorDto?> MatchVendorAsync(MatchQuery query, CancellationToken ct = default);
    Task UpdateVendorMasterAsync(string vendorNumber, VendorMasterPatch patch, CancellationToken ct = default);
}
