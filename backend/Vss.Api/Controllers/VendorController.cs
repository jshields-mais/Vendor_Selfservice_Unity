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
            .Include(x => x.CommunicationPreferences)
            .FirstOrDefaultAsync(x => x.Id == user.VendorId, ct);
        if (v is null) return NotFound();

        // The ERP is the system of record for banking and address, so refresh those before
        // display — the banking page loads the real payment method + active bank account, and
        // the addresses page reflects the PO Box / street shape held in the ERP.
        await RefreshFromErpAsync(v, ct);
        return VendorMapping.ToDto(v);
    }

    /// <summary>Business documents + channels offered for communication preferences.</summary>
    [HttpGet("communication-catalog")]
    public ActionResult<CommunicationCatalogDto> CommunicationCatalog()
    {
        var docs = Vss.Infrastructure.Erp.CommunicationCatalog.Documents
            .Select(d => new CommunicationCatalogDocDto(d.Name, d.ErpEnabled)).ToArray();
        return new CommunicationCatalogDto(docs, Vss.Infrastructure.Erp.CommunicationCatalog.Channels.ToArray());
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
        v.IsPoBox, v.PoBox, v.HouseNumber, v.RemitStreet, v.RemitCity, v.RemitState, v.RemitZip, v.RemitCountry);
}
