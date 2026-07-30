namespace Vss.Domain;

/// <summary>
/// A configurable, SAP-coded value list feeding the Contacts tab dropdowns. One row is one
/// allowed option in one of three lists (see <see cref="ContactCodeCategory"/>): the
/// <see cref="Code"/> is what we read from / write to SAP (e.g. Function "0016"), and the
/// <see cref="Description"/> is the human label shown in the dropdown. Maintained by City staff.
/// </summary>
public class ContactCode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Which of the three lists this row belongs to: Title, Department or Function.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>The SAP code, e.g. FormOfAddressCode "0001", FunctionalAreaCode "0002",
    /// FunctionTypeCode "0016". This is the value stored on the contact and sent to SAP.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable label shown in the dropdown, e.g. "Mr." or "Sales".</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Inactive codes are hidden from the vendor dropdown but kept for existing values.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Ascending display order within its list.</summary>
    public int SortOrder { get; set; }
}

/// <summary>The three contact code lists. Values are stored on <see cref="ContactCode.Category"/>.</summary>
public static class ContactCodeCategory
{
    /// <summary>Form of address / salutation → SAP FormOfAddressCode.</summary>
    public const string Title = "Title";
    /// <summary>Department → SAP BusinessPartnerFunctionalAreaCode.</summary>
    public const string Department = "Department";
    /// <summary>Function → SAP BusinessPartnerFunctionTypeCode.</summary>
    public const string Function = "Function";

    public static readonly string[] All = { Title, Department, Function };
    public static bool IsValid(string c) => Array.Exists(All, x => x == c);
}
