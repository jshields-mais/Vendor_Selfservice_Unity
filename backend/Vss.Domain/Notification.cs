namespace Vss.Domain;

/// <summary>
/// A per-document email notification the vendor wants to receive (Remittance Advice
/// Outbound / Purchase Order / Contract). Recipients live in the child
/// <see cref="NotificationRecipient"/> table (To / Cc / Bcc). Maps to a SAP ByDesign
/// supplier CommunicationArrangement (email medium).
/// </summary>
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VendorId { get; set; }

    /// <summary>Business document, e.g. "Purchase Order".</summary>
    public string Type { get; set; } = string.Empty;

    public List<NotificationRecipient> Recipients { get; set; } = new();
}

/// <summary>A single email recipient of a <see cref="Notification"/>, classified To/Cc/Bcc.</summary>
public class NotificationRecipient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotificationId { get; set; }

    /// <summary>"To", "Cc" or "Bcc".</summary>
    public string Kind { get; set; } = "To";
    public string Email { get; set; } = string.Empty;
}
