namespace Vss.Domain;

/// <summary>
/// Raw bytes of an uploaded file, addressed by <see cref="Id"/> (the storage reference
/// held on <see cref="VendorDocument.StorageRef"/>). This is the local stand-in for the
/// Unity "UDP drive": swapping <c>IDocumentStore</c> for a drive-backed implementation
/// moves the bytes off the database without touching callers.
/// </summary>
public class StoredFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
