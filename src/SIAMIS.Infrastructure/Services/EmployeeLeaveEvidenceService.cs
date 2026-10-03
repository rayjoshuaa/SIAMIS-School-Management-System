using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Leave;
using SIAMIS.Domain.Entities.Leave;

namespace SIAMIS.Infrastructure.Services;

public sealed partial class EmployeeLeaveService
{
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private async Task<LeaveEvidenceDto> EvidenceDto(EmployeeLeaveEvidence r, CancellationToken ct)
    {
        var events = await db.Set<EmployeeLeaveEvidenceEvent>().AsNoTracking().Where(x => x.EvidenceId == r.Id).OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).ToListAsync(ct);
        var state = events.Any(x => x.Action == LeaveEvidenceAction.Superseded) ? "Superseded"
            : events.Any(x => x.Action == LeaveEvidenceAction.Accepted) ? "Accepted"
            : events.Any(x => x.Action == LeaveEvidenceAction.Rejected) ? "Rejected" : "Recorded";
        return new(r.Id, r.EmployeeId, r.LeaveId, r.DocumentTypeId, r.EvidenceKind, r.ExternalReference, r.SupersedesEvidenceId,
            AsUtc(r.RecordedAt), state, events.Select(x => new LeaveEvidenceEventDto(x.Id, x.Action.ToString(), x.Remarks, AsUtc(x.OccurredAt))).ToArray());
    }
    public async Task<ServiceResult<LeaveEvidenceSummary>> EvidenceAsync(Guid employeeId, Guid leaveId, CancellationToken ct)
    {
        var leave = await db.EmployeeLeaves.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.LeaveId == leaveId, ct);
        if (leave is null) return Fail<LeaveEvidenceSummary>("not_found", "Leave request was not found for this employee.");
        var s = LeaveSnapshotIntegrity.Read(leave, await Allocations(leaveId, ct));
        if (!s.IsSuccess) return Fail<LeaveEvidenceSummary>("conflict", s.Failure!.Message);
        var types = LeaveEvidenceRules.RequiredTypes(s.Value!);
        if (!types.IsSuccess) return Fail<LeaveEvidenceSummary>("conflict", types.Failure!.Message);
        var receipts = await db.Set<EmployeeLeaveEvidence>().AsNoTracking().Where(x => x.EmployeeId == employeeId && x.LeaveId == leaveId).OrderBy(x => x.RecordedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var dtos = new List<LeaveEvidenceDto>(); foreach (var r in receipts) dtos.Add(await EvidenceDto(r, ct));
        var approval = await db.Set<EmployeeLeaveApprovalEvidence>().AsNoTracking().Where(x => x.EmployeeId == employeeId && x.LeaveId == leaveId).OrderBy(x => x.EvidenceId).Select(x => x.EvidenceId).ToListAsync(ct);
        return ServiceResult<LeaveEvidenceSummary>.Success(new(types.Value!, types.Value!.All(t => dtos.Any(x => x.DocumentTypeId == t && x.State == "Accepted")), approval, dtos));
    }
    public async Task<ServiceResult<LeaveEvidenceDto>> RecordEvidenceAsync(Guid employeeId, Guid leaveId, LeaveEvidenceRequest r, CancellationToken ct)
    {
        // External receipt tokens are not arbitrary paths, URLs or file names.
        if (!r.DocumentTypeId.HasValue || string.IsNullOrWhiteSpace(r.ExternalReference) || r.ExternalReference.Length > 200
            || !Regex.IsMatch(r.ExternalReference, "\\A[A-Za-z0-9][A-Za-z0-9_-]*\\z"))
            return Fail<LeaveEvidenceDto>("validation", "DocumentTypeId and an opaque external receipt token (letters, digits, hyphen or underscore, up to 200 characters) are required.");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await EmploymentIntegrity.LockAsync(db, employeeId, ct) is null) return Fail<LeaveEvidenceDto>("not_found", "Employee was not found.");
            var leave = await db.EmployeeLeaves.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.LeaveId == leaveId, ct);
            if (leave is null) return Fail<LeaveEvidenceDto>("not_found", "Leave request was not found for this employee.");
            if (!await db.DocumentTypes.AnyAsync(x => x.Id == r.DocumentTypeId && x.IsActive, ct)) return Fail<LeaveEvidenceDto>("validation", "DocumentTypeId must reference an active document type.");
            if (r.SupersedesEvidenceId.HasValue)
            {
                var previous = await db.Set<EmployeeLeaveEvidence>().SingleOrDefaultAsync(x => x.Id == r.SupersedesEvidenceId && x.EmployeeId == employeeId && x.LeaveId == leaveId, ct);
                if (previous is null) return Fail<LeaveEvidenceDto>("not_found", "Predecessor evidence was not found for this request.");
                if (previous.DocumentTypeId != r.DocumentTypeId) return Fail<LeaveEvidenceDto>("validation", "A successor must preserve the predecessor document type.");
                if (await db.Set<EmployeeLeaveEvidenceEvent>().AnyAsync(x => x.EvidenceId == previous.Id && x.Action == LeaveEvidenceAction.Superseded, ct)) return Fail<LeaveEvidenceDto>("conflict", "This evidence version is already superseded.");
                db.Add(new EmployeeLeaveEvidenceEvent { EvidenceId = previous.Id, Action = LeaveEvidenceAction.Superseded, Remarks = "Replaced by an immutable successor receipt." });
            }
            var receipt = new EmployeeLeaveEvidence { EmployeeId = employeeId, LeaveId = leaveId, DocumentTypeId = r.DocumentTypeId.Value, ExternalReference = r.ExternalReference, SupersedesEvidenceId = r.SupersedesEvidenceId };
            db.Add(receipt); db.Add(new EmployeeLeaveEvidenceEvent { EvidenceId = receipt.Id, Action = LeaveEvidenceAction.Recorded });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return ServiceResult<LeaveEvidenceDto>.Success(await EvidenceDto(receipt, ct));
        }
        catch (Exception e) when (Concurrent(e)) { return Fail<LeaveEvidenceDto>("conflict", "Evidence version changed concurrently. Retry the request."); }
    }
    public async Task<ServiceResult<LeaveEvidenceDto>> ReviewEvidenceAsync(Guid employeeId, Guid leaveId, Guid evidenceId, bool accept, LeaveEvidenceReviewRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.ReviewRemarks) || r.ReviewRemarks.Length > 2000) return Fail<LeaveEvidenceDto>("validation", "Explicit review remarks up to 2000 characters are required.");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await EmploymentIntegrity.LockAsync(db, employeeId, ct) is null) return Fail<LeaveEvidenceDto>("not_found", "Employee was not found.");
            var receipt = await db.Set<EmployeeLeaveEvidence>().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.LeaveId == leaveId && x.Id == evidenceId, ct);
            if (receipt is null) return Fail<LeaveEvidenceDto>("not_found", "Evidence was not found for this request.");
            if ((await EvidenceDto(receipt, ct)).State != "Recorded") return Fail<LeaveEvidenceDto>("conflict", "Only a current Recorded version can be accepted or rejected; create a successor to correct it.");
            db.Add(new EmployeeLeaveEvidenceEvent { EvidenceId = receipt.Id, Action = accept ? LeaveEvidenceAction.Accepted : LeaveEvidenceAction.Rejected, Remarks = r.ReviewRemarks.Trim() });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return ServiceResult<LeaveEvidenceDto>.Success(await EvidenceDto(receipt, ct));
        }
        catch (Exception e) when (Concurrent(e)) { return Fail<LeaveEvidenceDto>("conflict", "Evidence review changed concurrently. Retry the request."); }
    }
    private async Task<ServiceResult<bool>> FreezeApprovalEvidence(Guid employeeId, Guid leaveId, CancellationToken ct)
    {
        var evidence = await EvidenceAsync(employeeId, leaveId, ct);
        if (!evidence.IsSuccess) return Fail<bool>("conflict", evidence.Failure!.Message);
        if (!evidence.Value!.PrerequisitesSatisfied) return Fail<bool>("conflict", "Approval requires current Accepted external evidence for every frozen required document type.");
        foreach (var type in evidence.Value.RequiredDocumentTypeIds)
        {
            var r = evidence.Value.Receipts.Where(x => x.DocumentTypeId == type && x.State == "Accepted").OrderBy(x => x.RecordedAt).ThenBy(x => x.Id).First();
            db.Add(new EmployeeLeaveApprovalEvidence { EmployeeId = employeeId, LeaveId = leaveId, EvidenceId = r.Id });
        }
        return ServiceResult<bool>.Success(true);
    }
}
