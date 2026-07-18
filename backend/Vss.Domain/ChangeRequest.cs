namespace Vss.Domain;

/// <summary>
/// A proposed set of edits to one section of a vendor record. Vendors submit these;
/// City staff review the field-level diff and, on approval, the change is pushed to
/// the ERP vendor master via <c>IErpClient</c>.
/// </summary>
public class ChangeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-facing code, e.g. "CR-2043".</summary>
    public string Code { get; set; } = string.Empty;

    public Guid VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    /// <summary>Profile section this change targets, e.g. "Banking &amp; remittance".</summary>
    public string Section { get; set; } = string.Empty;

    /// <summary>For a document submission (Section = "Documents"), the uploaded document
    /// under review. On approval its file is attached to the ERP supplier master.</summary>
    public Guid? DocumentId { get; set; }

    public Guid SubmittedByUserId { get; set; }
    public string SubmittedByName { get; set; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.PendingReview;
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }

    public List<ChangeDiff> Diffs { get; set; } = new();
}

/// <summary>A single before/after field change within a <see cref="ChangeRequest"/>.</summary>
public class ChangeDiff
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChangeRequestId { get; set; }

    public string Field { get; set; } = string.Empty;
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
}
