using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vss.Api.Contracts;
using Vss.Domain;
using Vss.Infrastructure;
using Vss.Infrastructure.Documents;
using Vss.Infrastructure.Erp;

namespace Vss.Api.Controllers;

/// <summary>City-staff endpoints: change/link approval, vendors, and an ERP
/// connectivity check.</summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "Admin")]
public class AdminController(VssDbContext db, IErpClient erp, IOptions<ErpOptions> erpOptions, IDocumentStore store) : ControllerBase
{
    /// <summary>Pings the configured ERP (GetVendor on a sample id) and reports status.</summary>
    [HttpPost("erp/test")]
    public async Task<IActionResult> ErpTest(CancellationToken ct)
    {
        var opt = erpOptions.Value;
        var sample = opt.Provider.Equals("BusinessCentral", StringComparison.OrdinalIgnoreCase) ? opt.BusinessCentral.SampleVendorNumber
            : opt.Provider.Equals("SapByDesign", StringComparison.OrdinalIgnoreCase) ? opt.SapByDesign.SampleSupplierId
            : "V-10485";

        var sw = Stopwatch.StartNew();
        try
        {
            var v = await erp.GetVendorAsync(sample ?? "", ct);
            sw.Stop();
            return Ok(new
            {
                provider = opt.Provider,
                ok = true,
                latencyMs = sw.ElapsedMilliseconds,
                message = v is null ? $"Connected; sample '{sample}' not found" : $"Connected; found {v.Number} — {v.LegalName}",
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Ok(new { provider = opt.Provider, ok = false, latencyMs = sw.ElapsedMilliseconds, message = ex.Message });
        }
    }

    [HttpGet("change-requests")]
    public async Task<ActionResult<IEnumerable<ChangeRequestDto>>> ChangeRequests(CancellationToken ct)
    {
        var rows = await db.ChangeRequests.Include(c => c.Diffs).Include(c => c.Vendor)
            .OrderByDescending(c => c.SubmittedAt).ToListAsync(ct);
        return rows.Select(c => new ChangeRequestDto(
            c.Id, c.Code, c.Vendor?.LegalName ?? "", c.Section, c.SubmittedByName, c.SubmittedAt, c.Status.ToString(),
            c.Diffs.Select(d => new ChangeDiffDto(d.Field, d.FromValue, d.ToValue)).ToArray(), c.DocumentId)).ToList();
    }

    /// <summary>Approve a change request: apply the diff to the local record and push
    /// it to the ERP vendor master via <see cref="IErpClient"/>.</summary>
    [HttpPost("change-requests/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, ReviewDecisionDto? decision, CancellationToken ct)
    {
        var cr = await db.ChangeRequests.Include(c => c.Diffs).Include(c => c.Vendor)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cr is null) return NotFound();
        if (cr.Vendor is null) return BadRequest("Change request has no vendor.");

        var approvedAt = DateTimeOffset.UtcNow;

        if (cr.Section == "Documents" && cr.DocumentId is not null)
        {
            // Document submission: attach the uploaded file to the ERP supplier master.
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == cr.DocumentId, ct);
            if (doc?.StorageRef is not null)
            {
                var file = await store.GetAsync(doc.StorageRef, ct);
                if (file is not null)
                    await erp.AddSupplierAttachmentAsync(cr.Vendor.Number,
                        new ErpAttachment { FileName = file.FileName, MimeType = file.ContentType, Content = file.Content }, ct);
                doc.Status = DocumentStatus.Current;
            }
            cr.Vendor.LastSyncedAt = approvedAt;
        }
        else
        {
            var patch = new VendorMasterPatch { EffectiveDate = approvedAt };
            foreach (var d in cr.Diffs)
            {
                var prop = typeof(Vendor).GetProperty(d.Field);
                if (prop is not null && prop.PropertyType == typeof(string))
                    prop.SetValue(cr.Vendor, d.ToValue);
                patch.Fields[d.Field] = d.ToValue;
            }

            await erp.UpdateVendorMasterAsync(cr.Vendor.Number, patch, ct);
            cr.Vendor.LastSyncedAt = approvedAt;
        }

        cr.Status = ChangeRequestStatus.Approved;
        cr.DecidedAt = approvedAt;
        cr.DecisionNote = decision?.Note;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("change-requests/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, ReviewDecisionDto? decision, CancellationToken ct)
    {
        var cr = await db.ChangeRequests.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cr is null) return NotFound();
        cr.Status = ChangeRequestStatus.Rejected;
        cr.DecidedAt = DateTimeOffset.UtcNow;
        cr.DecisionNote = decision?.Note;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>A single change request (for the diff-review screen).</summary>
    [HttpGet("change-requests/{id:guid}")]
    public async Task<ActionResult<ChangeRequestDto>> ChangeRequest(Guid id, CancellationToken ct)
    {
        var c = await db.ChangeRequests.Include(x => x.Diffs).Include(x => x.Vendor)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        var docName = c.DocumentId is null ? null
            : (await db.Documents.FirstOrDefaultAsync(d => d.Id == c.DocumentId, ct))?.FileRef;
        return new ChangeRequestDto(c.Id, c.Code, c.Vendor?.LegalName ?? "", c.Section, c.SubmittedByName,
            c.SubmittedAt, c.Status.ToString(),
            c.Diffs.Select(d => new ChangeDiffDto(d.Field, d.FromValue, d.ToValue)).ToArray(), c.DocumentId, docName);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> Stats(CancellationToken ct) => new AdminStatsDto(
        "Online",
        await db.LinkRequests.CountAsync(l => l.Status == LinkRequestStatus.Pending || l.Status == LinkRequestStatus.Matched, ct),
        await db.ChangeRequests.CountAsync(c => c.Status == ChangeRequestStatus.PendingReview || c.Status == ChangeRequestStatus.InReview, ct),
        await db.VendorUsers.CountAsync(u => u.LinkState == LinkState.Linked, ct));

    [HttpGet("link-requests")]
    public async Task<ActionResult<IEnumerable<AdminLinkRequestDto>>> LinkRequests(CancellationToken ct)
    {
        var rows = await db.LinkRequests.Include(l => l.VendorUser)
            .OrderByDescending(l => l.CreatedAt).ToListAsync(ct);

        var numbers = rows.Where(r => r.MatchedVendorNumber != null).Select(r => r.MatchedVendorNumber!).Distinct().ToList();
        var names = await db.Vendors.Where(v => numbers.Contains(v.Number))
            .ToDictionaryAsync(v => v.Number, v => v.LegalName, ct);

        return rows.Select(r => new AdminLinkRequestDto(
            r.Id,
            (r.MatchedVendorNumber != null ? names.GetValueOrDefault(r.MatchedVendorNumber) : null) ?? r.VendorUser?.DisplayName ?? "",
            r.VendorUser?.Email ?? "",
            r.Method.ToString(),
            r.MatchedVendorNumber,
            r.CreatedAt,
            r.Status.ToString())).ToList();
    }

    /// <summary>Approve a link request: finalize the account ↔ vendor link.</summary>
    [HttpPost("link-requests/{id:guid}/approve")]
    public async Task<IActionResult> ApproveLink(Guid id, CancellationToken ct)
    {
        var lr = await db.LinkRequests.Include(l => l.VendorUser).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lr is null) return NotFound();
        if (lr.MatchedVendorNumber is null || lr.VendorUser is null) return BadRequest("Nothing to link.");

        var vendor = await db.Vendors.FirstOrDefaultAsync(v => v.Number == lr.MatchedVendorNumber, ct);
        if (vendor is null) return BadRequest("Matched vendor not found.");

        lr.VendorUser.VendorId = vendor.Id;
        lr.VendorUser.LinkState = LinkState.Linked;
        lr.Status = LinkRequestStatus.Approved;
        lr.DecidedAt = DateTimeOffset.UtcNow;
        lr.DecidedBy = "admin";
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("link-requests/{id:guid}/reject")]
    public async Task<IActionResult> RejectLink(Guid id, CancellationToken ct)
    {
        var lr = await db.LinkRequests.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lr is null) return NotFound();
        lr.Status = LinkRequestStatus.Rejected;
        lr.DecidedAt = DateTimeOffset.UtcNow;
        lr.DecidedBy = "admin";
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("vendors")]
    public async Task<ActionResult<IEnumerable<AdminVendorDto>>> Vendors(CancellationToken ct)
    {
        var rows = await db.Vendors.Include(v => v.CategoryCodes).OrderBy(v => v.Number).ToListAsync(ct);
        return rows.Select(v => new AdminVendorDto(
            v.Number,
            v.LegalName,
            v.CategoryCodes.FirstOrDefault()?.Code ?? "",
            v.LastSyncedAt,
            v.Status)).ToList();
    }
}
