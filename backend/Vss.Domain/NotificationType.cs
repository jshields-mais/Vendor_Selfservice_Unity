namespace Vss.Domain;

/// <summary>
/// A configurable notification type offered on the vendor Notifications tab (e.g. "Purchase
/// Order"). This is a VSS-owned config list maintained by City staff — not sourced from SAP.
/// Inactive types are hidden from vendors. <see cref="ErpServiceCode"/> is an optional SAP
/// CompoundServiceInterfaceCode: when set, the type's primary To address syncs to a SAP
/// CommunicationArrangement on approval; when empty the type is recorded in the portal only.
/// </summary>
public class NotificationType
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name shown to vendors (unique). Also the key on Notification.Type.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Inactive types are hidden from the Notifications tab but kept for history.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Ascending display order.</summary>
    public int SortOrder { get; set; }

    /// <summary>Optional SAP CompoundServiceInterfaceCode; empty = portal-only (no ERP write).</summary>
    public string? ErpServiceCode { get; set; }
}
