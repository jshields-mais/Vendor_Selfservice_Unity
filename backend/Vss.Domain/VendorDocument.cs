namespace Vss.Domain;

/// <summary>A compliance document attached to a vendor (W-9, COI, license, ...).</summary>
public class VendorDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VendorId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Original file name for display, e.g. "W-9.pdf". Null when nothing uploaded.</summary>
    public string? FileRef { get; set; }

    /// <summary>Opaque reference into <c>IDocumentStore</c> for the uploaded bytes.</summary>
    public string? StorageRef { get; set; }

    /// <summary>MIME type of the stored file (e.g. application/pdf).</summary>
    public string? ContentType { get; set; }

    public long? SizeBytes { get; set; }

    /// <summary>Human-readable validity, e.g. "No expiry" or "Exp. 12/31/2026".</summary>
    public string Validity { get; set; } = "—";

    public DocumentStatus Status { get; set; } = DocumentStatus.AwaitingDocs;
}
