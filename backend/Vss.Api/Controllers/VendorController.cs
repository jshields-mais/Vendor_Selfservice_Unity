using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vss.Api.Auth;
using Vss.Api.Contracts;
using Vss.Api.Mapping;
using Vss.Domain;
using Vss.Infrastructure;
using Vss.Infrastructure.Erp;

namespace Vss.Api.Controllers;

[ApiController]
[Route("api/v1/vendor")]
[Authorize]
public class VendorController(VssDbContext db, CurrentUser current, IErpClient erp, ILogger<VendorController> log) : ControllerBase
{
    /// <summary>The current user's linked vendor record (all sections, secrets masked).</summary>
    [HttpGet]
    public async Task<ActionResult<VendorDto>> Get(CancellationToken ct)
    {
        var user = await current.GetOrProvisionAsync(ct);
        if (user.VendorId is null)
            return StatusCode(StatusCodes.Status403Forbidden, "Account is not linked to a vendor record yet.");

        var v = await db.Vendors.Include(x => x.Documents).Include(x => x.CategoryCodes)
            .Include(x => x.Notifications).ThenInclude(n => n.Recipients)
            .Include(x => x.Contacts)
            .FirstOrDefaultAsync(x => x.Id == user.VendorId, ct);
        if (v is null) return NotFound();

        // The ERP is the system of record for banking and address, so refresh those before
        // display — the banking page loads the real payment method + active bank account, and
        // the addresses page reflects the PO Box / street shape held in the ERP.
        await RefreshFromErpAsync(v, ct);
        return VendorMapping.ToDto(v);
    }

    /// <summary>Notification types offered on the Notifications tab.</summary>
    [HttpGet("notification-catalog")]
    public async Task<ActionResult<NotificationCatalogDto>> NotificationCatalog(CancellationToken ct)
    {
        var types = await db.NotificationTypes.Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .Select(t => new NotificationTypeDto(t.Name, t.ErpServiceCode != null && t.ErpServiceCode != ""))
            .ToArrayAsync(ct);
        return new NotificationCatalogDto(types);
    }

    /// <summary>Active contact code lists (Title / Department / Function) for the Contacts dropdowns.</summary>
    [HttpGet("contact-codes")]
    public async Task<ActionResult<IEnumerable<ContactCodeDto>>> ContactCodes(CancellationToken ct)
    {
        var rows = await db.ContactCodes.Where(c => c.IsActive)
            .OrderBy(c => c.Category).ThenBy(c => c.SortOrder).ThenBy(c => c.Description).ToListAsync(ct);
        return rows.Select(c => new ContactCodeDto(c.Id, c.Category, c.Code, c.Description, c.IsActive, c.SortOrder)).ToList();
    }

    private async Task RefreshFromErpAsync(Vendor v, CancellationToken ct)
    {
        try
        {
            var e = await erp.GetVendorAsync(v.Number, ct);
            if (e is null) return;

            var before = Snapshot(v);

            // Banking (AccountType isn't reliably returned by SAP, so it's left local).
            if (!string.IsNullOrEmpty(e.PaymentMethod)) v.PaymentMethod = e.PaymentMethod;
            if (!string.IsNullOrEmpty(e.RoutingNumber)) v.RoutingNumber = e.RoutingNumber;
            if (!string.IsNullOrEmpty(e.AccountNumber)) v.AccountNumber = e.AccountNumber;

            // Contacts: SAP is the system of record. Reconcile the vendor's contact list against
            // the supplier's ContactPersons, matched by SAP UUID — update matches, add new ones,
            // remove ones SAP no longer has (only those already keyed to SAP).
            ReconcileContacts(v, e.Contacts);

            // Address (PO Box vs street are mutually exclusive in the ERP).
            v.IsPoBox = e.IsPoBox;
            v.PoBox = e.IsPoBox ? e.PoBox : null;
            v.HouseNumber = e.IsPoBox ? null : e.HouseNumber;
            v.RemitStreet = e.IsPoBox ? "" : (string.IsNullOrEmpty(e.RemitStreet) ? v.RemitStreet : e.RemitStreet);
            if (!string.IsNullOrEmpty(e.RemitCity)) v.RemitCity = e.RemitCity;
            if (!string.IsNullOrEmpty(e.RemitState)) v.RemitState = e.RemitState;
            if (!string.IsNullOrEmpty(e.RemitZip)) v.RemitZip = e.RemitZip;
            if (!string.IsNullOrEmpty(e.RemitCountry)) v.RemitCountry = e.RemitCountry;

            if (Snapshot(v) != before)
            {
                v.LastSyncedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            // A live ERP hiccup shouldn't break the profile page — serve the local copy.
            log.LogWarning(ex, "ERP refresh failed for vendor {Number}; serving local copy", v.Number);
        }
    }

    private static string Snapshot(Vendor v) => string.Join('|',
        v.PaymentMethod, v.RoutingNumber, v.AccountNumber,
        string.Join(";", v.Contacts.OrderBy(c => c.SapUuid ?? c.Id.ToString()).Select(c =>
            $"{c.SapUuid}:{c.IsPrimary}:{c.FirstName}:{c.LastName}:{c.Title}:{c.Function}:{c.Department}:{c.Email}:{c.Phone}:{c.Mobile}:{c.Fax}")),
        v.IsPoBox, v.PoBox, v.HouseNumber, v.RemitStreet, v.RemitCity, v.RemitState, v.RemitZip, v.RemitCountry);

    /// <summary>Reconciles the vendor's contact list with the supplier's SAP ContactPersons,
    /// matched by SAP UUID: update matches, add new ones, and remove SAP-keyed contacts that
    /// no longer exist in SAP. Contacts without a SAP UUID (not yet synced) are left alone.</summary>
    private void ReconcileContacts(Vendor v, List<ErpContact> erpContacts)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = 0;
        foreach (var e in erpContacts)
        {
            if (!string.IsNullOrEmpty(e.SapUuid)) seen.Add(e.SapUuid);
            var row = !string.IsNullOrEmpty(e.SapUuid)
                ? v.Contacts.FirstOrDefault(c => string.Equals(c.SapUuid, e.SapUuid, StringComparison.OrdinalIgnoreCase))
                : null;
            if (row is null)
            {
                row = new Contact { VendorId = v.Id, SapUuid = e.SapUuid };
                // Add via the DbSet so EF marks it Added (INSERT). Adding only through the tracked
                // nav collection makes DetectChanges infer Modified for the client-generated Guid
                // key → a 0-row UPDATE. EF relationship fixup also adds it to v.Contacts (VendorId
                // is set + v is tracked), so this request's DTO includes it — no explicit nav add.
                db.Contacts.Add(row);
            }
            row.SapInternalId = e.SapInternalId;
            row.IsPrimary = e.IsPrimary;
            row.FirstName = e.FirstName; row.LastName = e.LastName;
            row.Title = e.Title; row.Function = e.Function; row.Department = e.Department;
            row.Email = e.Email; row.Phone = e.Phone; row.Mobile = e.Mobile; row.Fax = e.Fax;
            row.SortOrder = order++;
        }
        // Drop SAP-keyed contacts SAP no longer returns (deleted upstream). Removing from the
        // tracked nav collection is enough — the required-FK cascade marks the row deleted;
        // an explicit DbSet.Remove as well would issue a second (0-row) DELETE.
        foreach (var stale in v.Contacts.Where(c => !string.IsNullOrEmpty(c.SapUuid) && !seen.Contains(c.SapUuid!)).ToList())
            v.Contacts.Remove(stale);
    }
}
