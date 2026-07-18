using Microsoft.EntityFrameworkCore;
using Vss.Domain;

namespace Vss.Infrastructure.Documents;

/// <summary>
/// Default <see cref="IDocumentStore"/> that keeps bytes in the database (<c>StoredFiles</c>).
/// Self-contained for local/dev and demos; the seam to swap for a UDP-drive-backed store
/// lives in DI (see <c>Program.cs</c>).
/// </summary>
public class DbDocumentStore(VssDbContext db) : IDocumentStore
{
    public async Task<string> SaveAsync(string fileName, string contentType, byte[] content, CancellationToken ct = default)
    {
        var file = new StoredFile { FileName = fileName, ContentType = contentType, Content = content };
        db.Set<StoredFile>().Add(file);
        await db.SaveChangesAsync(ct);
        return file.Id.ToString();
    }

    public async Task<StoredDocument?> GetAsync(string storageRef, CancellationToken ct = default)
    {
        if (!Guid.TryParse(storageRef, out var id)) return null;
        var f = await db.Set<StoredFile>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return f is null ? null : new StoredDocument(f.FileName, f.ContentType, f.Content);
    }
}
