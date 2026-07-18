using Microsoft.EntityFrameworkCore;
using Vss.Api.Contracts;
using Vss.Api.Mapping;
using Vss.Domain;
using Vss.Infrastructure;
using Vss.Infrastructure.Erp;

namespace Vss.Api.Services;

/// <summary>Shared read helpers used by more than one controller.</summary>
public static class PortalQueries
{
    public static async Task<MeDto> BuildMeAsync(VssDbContext db, string role, VendorUser user, CancellationToken ct)
    {
        Vendor? v = user.VendorId is null
            ? null
            : await db.Vendors.Include(x => x.Documents).Include(x => x.CategoryCodes)
                .FirstOrDefaultAsync(x => x.Id == user.VendorId, ct);

        var pct = v is null ? 0 : VendorMapping.CompletenessPct(v);
        var pending = v is null ? 0 : await db.ChangeRequests.CountAsync(
            c => c.VendorId == v.Id &&
                 (c.Status == ChangeRequestStatus.PendingReview || c.Status == ChangeRequestStatus.InReview), ct);

        return new MeDto(
            new UserDto(user.Id, user.Email, user.DisplayName, user.FirstName, user.LastName),
            user.LinkState.ToString(),
            role,
            v?.Number,
            v?.LegalName,
            pct,
            pending);
    }

    /// <summary>Build a portal Vendor from an ERP DTO (used when linking to a record
    /// not yet cached locally — the seeded dev data already exists, so this is the
    /// real-ERP path).</summary>
    public static Vendor FromErp(ErpVendorDto d) => new()
    {
        Number = d.Number,
        LegalName = d.LegalName,
        Dba = d.Dba,
        EntityType = d.EntityType,
        Website = d.Website,
        Status = d.Status,
        RemitStreet = d.RemitStreet,
        RemitCity = d.RemitCity,
        RemitState = d.RemitState,
        RemitZip = d.RemitZip,
        RemitCountry = d.RemitCountry,
        PaymentMethod = d.PaymentMethod,
        BankName = d.BankName,
        RoutingNumber = d.RoutingNumber,
        AccountNumber = d.AccountNumber,
        AccountType = d.AccountType,
        TaxIdType = d.TaxIdType,
        Tin = d.Tin,
        TaxClassification = d.TaxClassification,
        PrimaryContact = d.PrimaryContact,
        PrimaryEmail = d.PrimaryEmail,
        PrimaryPhone = d.PrimaryPhone,
        LastSyncedAt = DateTimeOffset.UtcNow,
        CategoryCodes = string.IsNullOrWhiteSpace(d.Category)
            ? new() : new() { new VendorCategoryCode { Code = d.Category } },
    };
}
