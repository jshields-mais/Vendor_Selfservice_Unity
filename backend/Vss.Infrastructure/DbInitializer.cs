using Microsoft.EntityFrameworkCore;
using Vss.Domain;

namespace Vss.Infrastructure;

/// <summary>Creates the schema (dev) and seeds demo data if the database is empty.</summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(VssDbContext db, CancellationToken ct = default)
    {
        // SQL Server (real/dev DB): apply migrations. Other providers used in tests
        // (SQLite in-memory) have no migrations — create the schema from the model.
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
            await db.Database.MigrateAsync(ct);
        else
            await db.Database.EnsureCreatedAsync(ct);

        if (!await db.Vendors.AnyAsync(ct))
            db.Vendors.AddRange(SeedData.Vendors());

        if (!await db.VendorUsers.AnyAsync(ct))
            db.VendorUsers.Add(SeedData.DanaUser());

        await db.SaveChangesAsync(ct);
    }
}
