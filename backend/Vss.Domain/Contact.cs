namespace Vss.Domain;

/// <summary>
/// One supplier contact — maps to a SAP ByDesign <c>ContactPerson</c> on the supplier. A vendor
/// can have many. Title / Function / Department are SAP-coded values (see the ContactCode config
/// lists). <see cref="SapUuid"/> / <see cref="SapInternalId"/> are the SAP record keys, null until
/// the contact has been created in SAP.
/// </summary>
public class Contact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VendorId { get; set; }

    /// <summary>SAP BusinessPartnerContactUUID (record key; null for a not-yet-synced contact).</summary>
    public string? SapUuid { get; set; }
    /// <summary>SAP BusinessPartnerContactInternalID.</summary>
    public string? SapInternalId { get; set; }
    /// <summary>SAP DefaultContactPersonIndicator — the supplier's primary contact.</summary>
    public bool IsPrimary { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    /// <summary>Title → SAP FormOfAddressCode (coded).</summary>
    public string? Title { get; set; }
    /// <summary>Function → SAP BusinessPartnerFunctionTypeCode (coded).</summary>
    public string? Function { get; set; }
    /// <summary>Department → SAP BusinessPartnerFunctionalAreaCode (coded).</summary>
    public string? Department { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Fax { get; set; }

    /// <summary>Display order in the grid (primary first, then insertion order).</summary>
    public int SortOrder { get; set; }
}
