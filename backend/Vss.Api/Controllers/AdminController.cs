using System.Diagnostics;
using System.Text.Json;
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
public class AdminController(VssDbContext db, IErpClient erp, IOptions<ErpOptions> erpOptions, IDocumentStore store, ErpConfigStore erpConfig) : ControllerBase
{
    /// <summary>Pings the configured ERP (GetVendor on a sample id) and reports status.</summary>
    [HttpPost("erp/test")]
    public async Task<IActionResult> ErpTest(CancellationToken ct)
    {
        var opt = erpOptions.Value;
        var row = erpConfig.Get();
        var sample = opt.Provider.Equals("BusinessCentral", StringComparison.OrdinalIgnoreCase) ? row.BcSampleVendorNumber
            : opt.Provider.Equals("SapByDesign", StringComparison.OrdinalIgnoreCase) ? row.SapSampleSupplierId
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

    /// <summary>The editable ERP connection config for the running provider (no secrets).</summary>
    [HttpGet("erp/config")]
    public ActionResult<ErpConfigDto> GetErpConfig()
    {
        var row = erpConfig.Get();
        var provider = erpConfig.Provider;
        var isBc = provider.Equals("BusinessCentral", StringComparison.OrdinalIgnoreCase);
        var isSap = provider.Equals("SapByDesign", StringComparison.OrdinalIgnoreCase);

        return isBc
            ? new ErpConfigDto(provider, "OAuth 2.0 (client credentials)", erpConfig.SecretConfigured(),
                row.BcBaseUrl, row.BcClientId, "", "", row.BcSampleVendorNumber, row.BcTenantId, row.BcScope, row.BcCompanyId, row.UpdatedAt)
            : new ErpConfigDto(provider, isSap ? "HTTP Basic" : "In-memory stub", erpConfig.SecretConfigured(),
                row.SapBaseUrl, row.SapUsername, row.SapQuerySupplierPath, row.SapManageSupplierPath, row.SapSampleSupplierId, "", "", "", row.UpdatedAt);
    }

    /// <summary>Persist the connection config for the running provider. Takes effect on the
    /// next request (SAP) — secrets are unchanged (set via user-secrets / env).</summary>
    [HttpPut("erp/config")]
    public async Task<ActionResult<ErpConfigDto>> UpdateErpConfig(ErpConfigUpdateDto dto, CancellationToken ct)
    {
        var row = erpConfig.Get();
        if (erpConfig.Provider.Equals("BusinessCentral", StringComparison.OrdinalIgnoreCase))
        {
            row.BcBaseUrl = dto.BaseUrl.Trim();
            row.BcClientId = dto.PrincipalId.Trim();
            row.BcSampleVendorNumber = dto.SampleId.Trim();
            row.BcTenantId = dto.TenantId.Trim();
            row.BcScope = dto.Scope.Trim();
            row.BcCompanyId = dto.CompanyId.Trim();
        }
        else
        {
            row.SapBaseUrl = dto.BaseUrl.Trim();
            row.SapUsername = dto.PrincipalId.Trim();
            row.SapQuerySupplierPath = dto.QuerySupplierPath.Trim();
            row.SapManageSupplierPath = dto.ManageSupplierPath.Trim();
            row.SapSampleSupplierId = dto.SampleId.Trim();
        }
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return GetErpConfig();
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
        else if (cr.Section == "Notifications")
        {
            // Each diff is "<Type> · <Kind>" (e.g. "Purchase Order · To") → comma/newline emails.
            var notifs = await db.Notifications.Include(n => n.Recipients)
                .Where(n => n.VendorId == cr.Vendor.Id).ToListAsync(ct);
            Notification Ensure(string type)
            {
                var n = notifs.FirstOrDefault(x => x.Type == type);
                if (n is null) { n = new Notification { VendorId = cr.Vendor.Id, Type = type }; db.Notifications.Add(n); notifs.Add(n); }
                return n;
            }
            var touchedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in cr.Diffs)
            {
                var parts = d.Field.Split(" · ", StringSplitOptions.TrimEntries);
                if (parts.Length != 2) continue;
                var (type, kind) = (parts[0], parts[1]);
                touchedTypes.Add(type);
                var emails = (d.ToValue ?? "").Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var n = Ensure(type);
                foreach (var r in n.Recipients.Where(r => r.Kind == kind).ToList()) n.Recipients.Remove(r);
                foreach (var em in emails) n.Recipients.Add(new NotificationRecipient { NotificationId = n.Id, Kind = kind, Email = em });
            }

            // SAP push, for every type this request touched: the primary To → CommunicationArrangement
            // email (enabled); a type left with no To disables its arrangement. Coded types only.
            var prefs = touchedTypes
                .Select(type => new
                {
                    Type = type,
                    To = notifs.FirstOrDefault(n => n.Type == type)?.Recipients
                        .Where(r => r.Kind == "To").Select(r => r.Email).FirstOrDefault(),
                })
                .Select(x => new ErpCommunicationPreference
                {
                    BusinessDocument = x.Type, Channel = "Email",
                    Email = x.To, Enabled = !string.IsNullOrEmpty(x.To),
                })
                .ToList();
            if (prefs.Count > 0) await erp.UpdateCommunicationPreferencesAsync(cr.Vendor.Number, prefs, ct);

            // Drop notifications left with no recipients (after the SAP push has read them).
            foreach (var n in notifs.Where(n => n.Recipients.Count == 0).ToList()) { db.Notifications.Remove(n); notifs.Remove(n); }
            cr.Vendor.LastSyncedAt = approvedAt;
        }
        else if (cr.Section == "Contacts")
        {
            // Each diff is one contact operation. field = "contact:<key>" where key is the VSS
            // contact Id (edit/delete) or "new" (add); toValue = JSON of the contact, or empty = delete.
            var contacts = await db.Contacts.Where(c => c.VendorId == cr.Vendor.Id).ToListAsync(ct);
            foreach (var d in cr.Diffs)
            {
                if (!d.Field.StartsWith("contact:", StringComparison.OrdinalIgnoreCase)) continue;
                var key = d.Field["contact:".Length..];
                var existing = contacts.FirstOrDefault(c => c.Id.ToString() == key);

                // Delete: no payload on an existing contact → remove from SAP + portal.
                if (existing is not null && string.IsNullOrWhiteSpace(d.ToValue))
                {
                    await erp.DeleteContactAsync(cr.Vendor.Number, existing.SapUuid, existing.SapInternalId, ct);
                    db.Contacts.Remove(existing);
                    continue;
                }

                var p = JsonSerializer.Deserialize<ContactPayloadDto>(d.ToValue ?? "{}",
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ContactPayloadDto(null, null, null, null, null, null, null, null, null);
                var target = existing ?? new Contact { VendorId = cr.Vendor.Id, SortOrder = contacts.Count };
                target.FirstName = p.FirstName; target.LastName = p.LastName;
                target.Title = p.Title; target.Function = p.Function; target.Department = p.Department;
                target.Email = p.Email; target.Phone = p.Phone; target.Mobile = p.Mobile; target.Fax = p.Fax;

                var res = await erp.UpsertContactAsync(cr.Vendor.Number, new ErpContact
                {
                    SapUuid = target.SapUuid, SapInternalId = target.SapInternalId, IsPrimary = target.IsPrimary,
                    FirstName = target.FirstName, LastName = target.LastName, Title = target.Title,
                    Function = target.Function, Department = target.Department, Email = target.Email,
                    Phone = target.Phone, Mobile = target.Mobile, Fax = target.Fax,
                }, ct);
                target.SapUuid = res.SapUuid ?? target.SapUuid;
                target.SapInternalId = res.SapInternalId ?? target.SapInternalId;
                if (existing is null) { db.Contacts.Add(target); contacts.Add(target); }
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

    // ---- Document types (configuration maintained by City staff) ----
    [HttpGet("document-types")]
    public async Task<ActionResult<IEnumerable<DocumentTypeDto>>> DocumentTypes(CancellationToken ct)
    {
        var rows = await db.DocumentTypes.OrderBy(t => t.SortOrder).ThenBy(t => t.Description).ToListAsync(ct);
        return rows.Select(t => new DocumentTypeDto(t.Id, t.Code, t.Description, t.IsActive, t.SortOrder)).ToList();
    }

    [HttpPost("document-types")]
    public async Task<ActionResult<DocumentTypeDto>> CreateDocumentType(DocumentTypeUpsertDto dto, CancellationToken ct)
    {
        var code = (dto.Code ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(dto.Description))
            return BadRequest("Code and description are required.");
        if (await db.DocumentTypes.AnyAsync(t => t.Code == code, ct))
            return Conflict($"Document type '{code}' already exists.");

        var t = new DocumentType { Code = code, Description = dto.Description.Trim(), IsActive = dto.IsActive, SortOrder = dto.SortOrder };
        db.DocumentTypes.Add(t);
        await db.SaveChangesAsync(ct);
        return new DocumentTypeDto(t.Id, t.Code, t.Description, t.IsActive, t.SortOrder);
    }

    [HttpPut("document-types/{id:guid}")]
    public async Task<ActionResult<DocumentTypeDto>> UpdateDocumentType(Guid id, DocumentTypeUpsertDto dto, CancellationToken ct)
    {
        var t = await db.DocumentTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();

        var code = (dto.Code ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(dto.Description))
            return BadRequest("Code and description are required.");
        if (await db.DocumentTypes.AnyAsync(x => x.Code == code && x.Id != id, ct))
            return Conflict($"Document type '{code}' already exists.");

        t.Code = code;
        t.Description = dto.Description.Trim();
        t.IsActive = dto.IsActive;
        t.SortOrder = dto.SortOrder;
        await db.SaveChangesAsync(ct);
        return new DocumentTypeDto(t.Id, t.Code, t.Description, t.IsActive, t.SortOrder);
    }

    [HttpDelete("document-types/{id:guid}")]
    public async Task<IActionResult> DeleteDocumentType(Guid id, CancellationToken ct)
    {
        var t = await db.DocumentTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();

        // Preserve history: if any uploaded document uses this code, deactivate instead of delete.
        if (await db.Documents.AnyAsync(d => d.DocumentTypeCode == t.Code, ct))
        {
            t.IsActive = false;
            await db.SaveChangesAsync(ct);
            return Ok(new { deactivated = true, message = "Type is in use; deactivated instead of deleted." });
        }

        db.DocumentTypes.Remove(t);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---- Contact code lists (Title / Department / Function — SAP-coded, City-maintained) ----
    [HttpGet("contact-codes")]
    public async Task<ActionResult<IEnumerable<ContactCodeDto>>> ContactCodes(CancellationToken ct)
    {
        var rows = await db.ContactCodes.OrderBy(c => c.Category).ThenBy(c => c.SortOrder).ThenBy(c => c.Description).ToListAsync(ct);
        return rows.Select(c => new ContactCodeDto(c.Id, c.Category, c.Code, c.Description, c.IsActive, c.SortOrder)).ToList();
    }

    [HttpPost("contact-codes")]
    public async Task<ActionResult<ContactCodeDto>> CreateContactCode(ContactCodeUpsertDto dto, CancellationToken ct)
    {
        var (category, code, err) = NormalizeContactCode(dto);
        if (err is not null) return BadRequest(err);
        if (await db.ContactCodes.AnyAsync(c => c.Category == category && c.Code == code, ct))
            return Conflict($"{category} code '{code}' already exists.");

        var c = new ContactCode { Category = category, Code = code, Description = dto.Description.Trim(), IsActive = dto.IsActive, SortOrder = dto.SortOrder };
        db.ContactCodes.Add(c);
        await db.SaveChangesAsync(ct);
        return new ContactCodeDto(c.Id, c.Category, c.Code, c.Description, c.IsActive, c.SortOrder);
    }

    [HttpPut("contact-codes/{id:guid}")]
    public async Task<ActionResult<ContactCodeDto>> UpdateContactCode(Guid id, ContactCodeUpsertDto dto, CancellationToken ct)
    {
        var c = await db.ContactCodes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        var (category, code, err) = NormalizeContactCode(dto);
        if (err is not null) return BadRequest(err);
        if (await db.ContactCodes.AnyAsync(x => x.Category == category && x.Code == code && x.Id != id, ct))
            return Conflict($"{category} code '{code}' already exists.");

        c.Category = category;
        c.Code = code;
        c.Description = dto.Description.Trim();
        c.IsActive = dto.IsActive;
        c.SortOrder = dto.SortOrder;
        await db.SaveChangesAsync(ct);
        return new ContactCodeDto(c.Id, c.Category, c.Code, c.Description, c.IsActive, c.SortOrder);
    }

    [HttpDelete("contact-codes/{id:guid}")]
    public async Task<IActionResult> DeleteContactCode(Guid id, CancellationToken ct)
    {
        var c = await db.ContactCodes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        db.ContactCodes.Remove(c);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static (string category, string code, string? error) NormalizeContactCode(ContactCodeUpsertDto dto)
    {
        var category = (dto.Category ?? "").Trim();
        var code = (dto.Code ?? "").Trim();
        if (!ContactCodeCategory.IsValid(category))
            return ("", "", $"Category must be one of: {string.Join(", ", ContactCodeCategory.All)}.");
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(dto.Description))
            return ("", "", "Code and description are required.");
        return (category, code, null);
    }

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
