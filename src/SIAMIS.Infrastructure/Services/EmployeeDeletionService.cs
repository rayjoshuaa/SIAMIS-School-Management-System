using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Security;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Security;

namespace SIAMIS.Infrastructure.Services;

public sealed class EmployeeDeletionService(SIAMISDbContext db, ICurrentActor actor) : IEmployeeDeletionService
{
    private bool Authorized => actor.UserId.HasValue && actor.HasCapability("Employee.DeletePermanent") && actor.HasCapability("Employee.Read");
    private static readonly HashSet<string> RegistrationChildren = ["EmploymentRecords.EmployeeId", "EmployeeContacts.EmployeeId", "EmployeeAddresses.EmployeeId", "EmergencyContacts.EmployeeId"];
    private static readonly HashSet<string> KnownReferences = [
        "Attendance.EmployeeId", "AttendanceEvents.EmployeeId", "AttendanceReviewActions.EmployeeId", "AttendanceReviewCases.EmployeeId",
        "EmergencyContacts.EmployeeId", "EmployeeAddresses.EmployeeId", "EmployeeClockSessions.EmployeeId", "EmployeeCompensations.EmployeeId",
        "EmployeeContacts.EmployeeId", "EmployeeContracts.EmployeeId", "EmployeeDocuments.EmployeeId", "EmployeeDocuments.VerifiedBy",
        "EmployeeHistory.EmployeeId", "EmployeeLeave.EmployeeId", "EmployeeLeaveEntitlements.EmployeeId", "EmployeePayrollComponentAssignments.EmployeeId",
        "EmployeePayrollPitResults.EmployeeId", "EmployeePayrolls.EmployeeId", "EmployeePerformance.EmployeeId", "EmployeePerformance.ReviewerEmployeeId",
        "EmployeePhotoRevisions.EmployeeId", "EmployeePitPaymentSchedules.EmployeeId", "EmployeeStatutoryEnrollments.EmployeeId",
        "EmployeeTaxDeclarations.EmployeeId", "EmployeeTaxProfiles.EmployeeId", "EmployeeWorkCalendarAssignments.EmployeeId",
        "EmploymentRecords.EmployeeId", "EmploymentRecords.ReportingToEmployeeId", "FinalizedAttendanceRevisions.EmployeeId", "TeacherProfiles.EmployeeId", "Users.EmployeeId"];

    public async Task<ServiceResult<EmployeeDeletionAssessment>> AssessAsync(Guid id, CancellationToken ct)
    {
        if (!Authorized) return ServiceResult<EmployeeDeletionAssessment>.Fail("forbidden", "Permanent deletion authorization is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await AssessLockedAsync(id, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task<ServiceResult<EmployeeDeletionAssessment>> AssessLockedAsync(Guid id, CancellationToken ct)
    {
        var employee = await db.Employees.FromSqlInterpolated($"SELECT * FROM Employees WITH (XLOCK,HOLDLOCK) WHERE EmployeeId={id}").SingleOrDefaultAsync(ct);
        if (employee is null) return ServiceResult<EmployeeDeletionAssessment>.Fail("not_found", "Employee was not found.");
        var blockers = new SortedSet<string>();
        var triggers = await SqlRowsAsync("SELECT COUNT(*) FROM sys.triggers WHERE name IN ('TR_Employees_PermanentIdentity','TR_EmployeeNumberReservations_Permanent') AND is_disabled=0", null, ct);
        if (triggers[0][0] != "2") blockers.Add("identity_requires_review");
        var facts = new StringBuilder(JsonSerializer.Serialize(new { employee.EmployeeId, employee.EmployeeNumber, employee.IsActive, employee.UpdatedAt }));
        var reservation = await db.EmployeeNumberReservations.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == id, ct);
        if (reservation is null || reservation.EmployeeNumber != employee.EmployeeNumber || reservation.AssignedAtUtc is null || reservation.RetiredAtUtc is not null)
            blockers.Add("identity_requires_review");
        facts.Append(JsonSerializer.Serialize(reservation));
        var history = await db.EmploymentRecords.AsNoTracking().Where(x => x.EmployeeId == id).OrderBy(x => x.EmploymentRecordId).ToListAsync(ct);
        facts.Append(JsonSerializer.Serialize(history));
        if (!employee.IsActive || history.Count != 1 || !history[0].IsCurrent || history[0].EndDate.HasValue)
            blockers.Add("employment_history");
        if (employee.ProfilePhoto is not null) blockers.Add("protected_records");

        var ownershipPrefix = $"EmployeeId={id};";
        var audit = await db.Set<SecurityAuditEvent>().AsNoTracking().Where(x => x.Operation.StartsWith(ownershipPrefix)
            || x.ResourceType == "Employee" && x.ResourceId == id.ToString()).OrderBy(x => x.Id).ToListAsync(ct);
        if (!audit.Any(x => x.ActorUserId.HasValue && x.ResourceType == "Employee" && x.ResourceId == id.ToString()
                && x.Operation.StartsWith(ownershipPrefix) && x.Operation.EndsWith(":Added"))
            || history.Count != 1 || !audit.Any(x => x.ActorUserId.HasValue && x.ResourceType == "EmploymentRecord"
                && x.ResourceId == history[0].EmploymentRecordId.ToString() && x.Operation.EndsWith(":Added")))
            blockers.Add("registration_provenance_missing");
        foreach (var entry in audit.Where(x => x.Operation.StartsWith(ownershipPrefix)))
            if (entry.ResourceType == "EmploymentRecord" && !entry.Operation.EndsWith(":Added")
                || entry.ResourceType is not ("Employee" or "EmploymentRecord" or "EmployeeContact" or "EmployeeAddress" or "EmergencyContact"))
                blockers.Add("protected_history");
        facts.Append(JsonSerializer.Serialize(audit.Where(x => !x.Operation.StartsWith("PermanentDeletion")).Select(x => x.Id)));
        if (audit.Any(x => !x.Operation.StartsWith(ownershipPrefix) && !x.Operation.StartsWith("PermanentDeletion")))
            blockers.Add("audit_requires_review");

        // Inspect the actual catalog, not just EF navigation properties. Unknown references are never silently ignored.
        var references = await SqlRowsAsync("SELECT SCHEMA_NAME(t.schema_id), t.name, c.name, f.delete_referential_action_desc, f.is_disabled, f.is_not_trusted FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id JOIN sys.tables t ON t.object_id=f.parent_object_id JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=fc.parent_column_id WHERE f.referenced_object_id=OBJECT_ID('dbo.Employees') ORDER BY t.name,c.name", null, ct);
        if (references.Count != KnownReferences.Count) blockers.Add("schema_requires_review");
        foreach (var reference in references)
        {
            string schema = reference[0], table = reference[1], column = reference[2], key = table + "." + column;
            if (schema != "dbo" || !KnownReferences.Contains(key) || reference[3] != "NO_ACTION" || reference[4] != "False" || reference[5] != "False")
                blockers.Add("schema_requires_review");
            var rows = await SqlRowsAsync($"SELECT COUNT_BIG(*) FROM {Quote(schema)}.{Quote(table)} WITH (UPDLOCK,HOLDLOCK) WHERE {Quote(column)}=@id", id, ct);
            long count = long.Parse(rows[0][0], System.Globalization.CultureInfo.InvariantCulture);
            facts.Append(key).Append(count);
            if (count > 0 && !RegistrationChildren.Contains(key))
                blockers.Add(table == "Users" ? "linked_account" : "protected_records");
        }
        var targets = await SqlRowsAsync("SELECT COUNT_BIG(*) FROM PayrollRuleTargets WITH (UPDLOCK,HOLDLOCK) WHERE TargetType='Employee' AND TargetId=@id", id, ct);
        facts.Append("targets").Append(targets[0][0]);
        if (targets[0][0] != "0") blockers.Add(actor.HasCapability("Payroll.Read") ? "payroll_reference" : "protected_records");
        // Values, not just counts, protect against a stale preview of permissible registration children.
        foreach (var pair in new[] { ("EmployeeContacts", "EmployeeContactId"), ("EmployeeAddresses", "EmployeeAddressId"), ("EmergencyContacts", "EmergencyContactId") })
            foreach (var row in await SqlRowsAsync($"SELECT * FROM {Quote(pair.Item1)} WITH (UPDLOCK,HOLDLOCK) WHERE EmployeeId=@id ORDER BY {Quote(pair.Item2)} FOR JSON PATH,INCLUDE_NULL_VALUES", id, ct)) facts.Append(row[0]);
        var state = blockers.Any(x => x.EndsWith("review") || x == "registration_provenance_missing") ? "ReviewRequired" : blockers.Count > 0 ? "Blocked" : "Eligible";
        return ServiceResult<EmployeeDeletionAssessment>.Success(new(state, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts.ToString()))), blockers.ToArray()));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid id, EmployeeDeletionRequest request, CancellationToken ct)
    {
        string outcome = "Denied";
        try
        {
            if (!Authorized) return ServiceResult<bool>.Fail("forbidden", "Permanent deletion authorization is required.");
            if (!Validator.TryValidateObject(request, new ValidationContext(request), [], true) || request.Reason.Trim().Length < 5
                || request.Reason.Any(char.IsControl)) return ServiceResult<bool>.Fail("validation", "Provide the employee number, current assessment and a reason of 5–120 characters.");
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var assessment = await AssessLockedAsync(id, ct);
            if (!assessment.IsSuccess) return ServiceResult<bool>.Fail(assessment.Failure!.Code, assessment.Failure.Message);
            if (assessment.Value!.State != "Eligible" || assessment.Value.Version != request.Version)
                return ServiceResult<bool>.Fail("conflict", "Deletion is blocked or the assessment changed. Reload the dependency assessment.");
            var employee = await db.Employees.SingleAsync(x => x.EmployeeId == id, ct);
            if (!string.Equals(employee.EmployeeNumber, request.EmployeeNumber, StringComparison.Ordinal))
                return ServiceResult<bool>.Fail("validation", "Type the exact employee number.");
            db.EmploymentRecords.RemoveRange(await db.EmploymentRecords.Where(x => x.EmployeeId == id).ToListAsync(ct));
            db.EmployeeContacts.RemoveRange(await db.EmployeeContacts.Where(x => x.EmployeeId == id).ToListAsync(ct));
            db.EmployeeAddresses.RemoveRange(await db.EmployeeAddresses.Where(x => x.EmployeeId == id).ToListAsync(ct));
            db.EmergencyContacts.RemoveRange(await db.EmergencyContacts.Where(x => x.EmployeeId == id).ToListAsync(ct));
            db.Employees.Remove(employee);
            db.Add(new SecurityAuditEvent { ActorUserId = actor.UserId, ResourceType = "Employee", ResourceId = id.ToString(),
                Operation = $"PermanentDeleted;Number={employee.EmployeeNumber};Reason={request.Reason.Trim()}" });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            outcome = "Success";
            return ServiceResult<bool>.Success(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { outcome = "Failed"; return ServiceResult<bool>.Fail("conflict", "Deletion did not complete. Reload before retrying."); }
        finally
        {
            // A fresh context preserves denied/failed attempt evidence after the mutation transaction rolls back.
            if (outcome != "Success") await AuditAsync(id, outcome, CancellationToken.None);
        }
    }
    public Task AuditDeniedAsync(Guid id, CancellationToken ct) => AuditAsync(id, "AuthorizationDenied", ct);
    public Task AuditAttemptAsync(Guid id, CancellationToken ct) => AuditAsync(id, "Attempt", ct);
    private async Task AuditAsync(Guid id, string outcome, CancellationToken ct)
    {
        await using var auditDb = new SIAMISDbContext(new DbContextOptionsBuilder<SIAMISDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
        auditDb.Add(new SecurityAuditEvent { ActorUserId = actor.UserId, ResourceType = "Employee", ResourceId = id.ToString(), Operation = "PermanentDeletion" + outcome });
        await auditDb.SaveChangesAsync(ct);
    }
    private static string Quote(string name) => "[" + name.Replace("]", "]]") + "]";
    private async Task<List<string[]>> SqlRowsAsync(string sql, Guid? id, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        if (id.HasValue) { var parameter = command.CreateParameter(); parameter.ParameterName = "@id"; parameter.Value = id.Value; command.Parameters.Add(parameter); }
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<string[]>();
        while (await reader.ReadAsync(ct)) rows.Add(Enumerable.Range(0, reader.FieldCount).Select(i => Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture)!).ToArray());
        return rows;
    }
}
