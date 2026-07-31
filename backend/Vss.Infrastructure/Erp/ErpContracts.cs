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

    public bool IsPoBox { get; set; }
    public string? PoBox { get; set; }
    public string RemitStreet { get; set; } = string.Empty;
    public string? HouseNumber { get; set; }
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

    // Contacts (each a SAP ContactPerson on the supplier)
    public List<ErpContact> Contacts { get; set; } = new();

    // Supplier-level address email/phone (not the contact)
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

    /// <summary>When the change was approved in VSS. Drives SAP bank-detail validity
    /// dating: a new/changed bank account becomes valid from this date. Null for writes
    /// that don't need an effective date.</summary>
    public DateTimeOffset? EffectiveDate { get; set; }
}

/// <summary>A file to attach to the ERP vendor master (supplier "Attachments" tab).</summary>
public class ErpAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/pdf";
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

/// <summary>One supplier contact = a SAP ContactPerson. Keys are null for a not-yet-created contact.</summary>
public class ErpContact
{
    public string? SapUuid { get; set; }
    public string? SapInternalId { get; set; }
    public bool IsPrimary { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Title { get; set; }       // FormOfAddressCode
    public string? Function { get; set; }     // BusinessPartnerFunctionTypeCode
    public string? Department { get; set; }   // BusinessPartnerFunctionalAreaCode
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Fax { get; set; }
}

/// <summary>Keys returned after creating/updating a contact in the ERP.</summary>
public record ErpContactResult(string? SapUuid, string? SapInternalId);

/// <summary>A supplier's preferred delivery channel for one business document.</summary>
public class ErpCommunicationPreference
{
    public string BusinessDocument { get; set; } = string.Empty; // e.g. "Purchase Order"
    public string Channel { get; set; } = string.Empty;          // e.g. "Email"
    public string? Email { get; set; }
    /// <summary>False when the vendor cleared all recipients → disable the SAP arrangement.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>SAP CompoundServiceInterfaceCode from the notification-type config; null/empty
    /// means the type has no ERP mapping and is skipped for the SAP write.</summary>
    public string? ServiceInterfaceCode { get; set; }
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

    /// <summary>Adds a document to the supplier's attachments. Returns false if the
    /// provider doesn't support attachments (the caller keeps the file in the portal).</summary>
    Task<bool> AddSupplierAttachmentAsync(string vendorNumber, ErpAttachment attachment, CancellationToken ct = default);

    /// <summary>Writes the supplier's per-document delivery preferences. Returns the number
    /// of preferences pushed to the ERP (0 if the provider/document isn't ERP-enabled).</summary>
    Task<int> UpdateCommunicationPreferencesAsync(string vendorNumber, IReadOnlyList<ErpCommunicationPreference> preferences, CancellationToken ct = default);

    /// <summary>Creates (no keys) or updates (with SapUuid) a supplier ContactPerson.
    /// Returns the SAP record keys so a newly created contact can be tracked.</summary>
    Task<ErpContactResult> UpsertContactAsync(string vendorNumber, ErpContact contact, CancellationToken ct = default);

    /// <summary>Deletes a supplier ContactPerson identified by its SAP keys.</summary>
    Task DeleteContactAsync(string vendorNumber, string? sapUuid, string? sapInternalId, CancellationToken ct = default);
}
