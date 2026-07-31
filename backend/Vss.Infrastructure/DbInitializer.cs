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

        if (!await db.DocumentTypes.AnyAsync(ct))
            db.DocumentTypes.AddRange(SeedData.DocumentTypes());

        if (!await db.ContactCodes.AnyAsync(ct))
            db.ContactCodes.AddRange(SeedData.ContactCodes());

        if (!await db.NotificationTypes.AnyAsync(ct))
            db.NotificationTypes.AddRange(SeedData.NotificationTypes());

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Seed the editable ERP config from options if it hasn't been set yet.</summary>
    public static async Task SeedErpConfigAsync(VssDbContext db, Erp.ErpOptions options, CancellationToken ct = default)
    {
        if (!await db.ErpConfigs.AnyAsync(ct))
        {
            db.ErpConfigs.Add(Erp.ErpConfigStore.FromOptions(options));
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Dev-only: wipe all data and restore canonical seed data.</summary>
    public static async Task ReseedAsync(VssDbContext db, CancellationToken ct = default)
    {
        // Delete children before parents (FK-safe).
        await db.ChangeDiffs.ExecuteDeleteAsync(ct);
        await db.ChangeRequests.ExecuteDeleteAsync(ct);
        await db.LinkRequests.ExecuteDeleteAsync(ct);
        await db.Documents.ExecuteDeleteAsync(ct);
        await db.CategoryCodes.ExecuteDeleteAsync(ct);
        await db.NotificationRecipients.ExecuteDeleteAsync(ct);
        await db.Notifications.ExecuteDeleteAsync(ct);
        await db.Contacts.ExecuteDeleteAsync(ct);
        await db.VendorUsers.ExecuteDeleteAsync(ct);
        await db.Vendors.ExecuteDeleteAsync(ct);

        db.Vendors.AddRange(SeedData.Vendors());
        db.VendorUsers.Add(SeedData.DanaUser());
        if (!await db.DocumentTypes.AnyAsync(ct))
            db.DocumentTypes.AddRange(SeedData.DocumentTypes());
        if (!await db.ContactCodes.AnyAsync(ct))
            db.ContactCodes.AddRange(SeedData.ContactCodes());
        if (!await db.NotificationTypes.AnyAsync(ct))
            db.NotificationTypes.AddRange(SeedData.NotificationTypes());
        await db.SaveChangesAsync(ct);
    }
}
