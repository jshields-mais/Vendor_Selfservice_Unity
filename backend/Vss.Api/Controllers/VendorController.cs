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
    public ActionResult<NotificationCatalogDto> NotificationCatalog()
    {
        var types = Vss.Infrastructure.Erp.CommunicationCatalog.Documents
            .Select(d => new NotificationTypeDto(d.Name, d.ErpEnabled)).ToArray();
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

            // Contact: the supplier's default ContactPerson (name, title, dept, function, email/phone/fax).
            if (!string.IsNullOrEmpty(e.ContactFirstName)) v.ContactFirstName = e.ContactFirstName;
            if (!string.IsNullOrEmpty(e.ContactLastName)) v.ContactLastName = e.ContactLastName;
            // Title / Function / Department are SAP-coded and read atomically with the contact,
            // so mirror SAP exactly — including clearing a value SAP no longer carries.
            v.ContactTitle = e.ContactTitle;
            v.ContactFunction = e.ContactFunction;
            v.ContactDepartment = e.ContactDepartment;
            if (!string.IsNullOrEmpty(e.ContactEmail)) v.ContactEmail = e.ContactEmail;
            if (!string.IsNullOrEmpty(e.ContactPhone)) v.ContactPhone = e.ContactPhone;
            if (!string.IsNullOrEmpty(e.ContactMobile)) v.ContactMobile = e.ContactMobile;
            if (!string.IsNullOrEmpty(e.ContactFax)) v.ContactFax = e.ContactFax;

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
        v.ContactFirstName, v.ContactLastName, v.ContactTitle, v.ContactFunction, v.ContactDepartment, v.ContactEmail, v.ContactPhone, v.ContactMobile, v.ContactFax,
        v.IsPoBox, v.PoBox, v.HouseNumber, v.RemitStreet, v.RemitCity, v.RemitState, v.RemitZip, v.RemitCountry);
}
