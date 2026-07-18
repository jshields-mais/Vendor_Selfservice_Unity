namespace Vss.Infrastructure.Documents;

/// <summary>A stored document's bytes plus the metadata needed to serve it back.</summary>
public record StoredDocument(string FileName, string ContentType, byte[] Content);

/// <summary>
/// Storage boundary for uploaded documents. The portal keeps only a storage reference on
/// the <c>VendorDocument</c> and reads/writes the bytes through this interface, so the
/// backing store can change without touching callers.
///
/// The default <see cref="DbDocumentStore"/> keeps bytes in the database for local/dev.
/// In a Unity deployment this is where a UDP-drive-backed implementation plugs in
/// (upload to the drive, return its reference; fetch by reference for preview/attach).
/// </summary>
public interface IDocumentStore
{
    /// <summary>Persists file bytes and returns an opaque storage reference.</summary>
    Task<string> SaveAsync(string fileName, string contentType, byte[] content, CancellationToken ct = default);

    /// <summary>Fetches previously stored bytes by reference, or null if not found.</summary>
    Task<StoredDocument?> GetAsync(string storageRef, CancellationToken ct = default);
}
