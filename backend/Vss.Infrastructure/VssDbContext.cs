using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Vss.Domain;

namespace Vss.Infrastructure;

public class VssDbContext(DbContextOptions<VssDbContext> options) : DbContext(options)
{
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<VendorCategoryCode> CategoryCodes => Set<VendorCategoryCode>();
    public DbSet<VendorDocument> Documents => Set<VendorDocument>();
    public DbSet<VendorUser> VendorUsers => Set<VendorUser>();
    public DbSet<LinkRequest> LinkRequests => Set<LinkRequest>();
    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();
    public DbSet<ChangeDiff> ChangeDiffs => Set<ChangeDiff>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<ErpConfig> ErpConfigs => Set<ErpConfig>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();
    public DbSet<ContactCode> ContactCodes => Set<ContactCode>();
    public DbSet<Contact> Contacts => Set<Contact>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite can't ORDER BY / compare DateTimeOffset stored as TEXT. Store as a
        // sortable binary (long) so ordering and range queries work on all providers.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Vendor>(e =>
        {
            e.HasIndex(v => v.Number).IsUnique();
            e.HasMany(v => v.CategoryCodes).WithOne().HasForeignKey(c => c.VendorId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(v => v.Documents).WithOne().HasForeignKey(d => d.VendorId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(v => v.Notifications).WithOne().HasForeignKey(n => n.VendorId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(v => v.Contacts).WithOne().HasForeignKey(c => c.VendorId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Notification>()
            .HasMany(n => n.Recipients).WithOne().HasForeignKey(r => r.NotificationId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<VendorUser>(e =>
        {
            e.HasIndex(u => u.ExternalUuid).IsUnique();
            e.HasOne(u => u.Vendor).WithMany().HasForeignKey(u => u.VendorId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<LinkRequest>()
            .HasOne(l => l.VendorUser).WithMany().HasForeignKey(l => l.VendorUserId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<ChangeRequest>(e =>
        {
            e.HasIndex(c => c.Code).IsUnique();
            e.HasOne(c => c.Vendor).WithMany().HasForeignKey(c => c.VendorId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(c => c.Diffs).WithOne().HasForeignKey(d => d.ChangeRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DocumentType>(e => e.HasIndex(t => t.Code).IsUnique());

        // One code is unique within its list (the same code may recur across categories).
        b.Entity<ContactCode>(e => e.HasIndex(c => new { c.Category, c.Code }).IsUnique());
    }
}
