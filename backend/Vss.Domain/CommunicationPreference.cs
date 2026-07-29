namespace Vss.Domain;

/// <summary>
/// A vendor contact's preferred delivery channel for a specific business document
/// (e.g. Purchase Order → Email). Maps to a SAP ByDesign supplier
/// <c>CommunicationArrangement</c> (CompoundServiceInterfaceCode + CommunicationMediumTypeCode).
/// </summary>
public class CommunicationPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VendorId { get; set; }

    /// <summary>Business document display name, e.g. "Purchase Order".</summary>
    public string BusinessDocument { get; set; } = string.Empty;

    /// <summary>Preferred channel display name, e.g. "Email".</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>Email address for the email channel (EMailURI); null for other channels.</summary>
    public string? Email { get; set; }
}
