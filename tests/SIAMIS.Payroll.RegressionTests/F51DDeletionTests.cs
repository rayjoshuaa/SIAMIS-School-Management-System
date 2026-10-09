using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Security;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Security;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Payroll;
using Microsoft.AspNetCore.Identity;

internal static class F51DDeletionTests
{
    public static async Task RunAsync(HttpClient admin, HttpClient hr, HttpClient payroll, HttpClient anonymous, Dictionary<string, object?> input, IServiceProvider services)
    {
        int count = 0;
        void Check(bool value, string message) { if (!value) throw new Exception("FAIL: " + message); count++; Console.WriteLine("PASS: " + message); }
        await using (var modelScope = services.CreateAsyncScope())
            Check(!modelScope.ServiceProvider.GetRequiredService<SIAMISDbContext>().Database.HasPendingModelChanges(), "EF model has no pending changes");
        Check(SecurityCapabilities.ForRoles(["SystemAdmin"]).Contains("Employee.DeletePermanent"), "explicit SystemAdmin deletion grant");
        foreach (var role in new[] { "HRAdmin", "PayrollAdmin", "Management", "Employee" })
            Check(!SecurityCapabilities.ForRoles([role]).Contains("Employee.DeletePermanent"), role + " has no deletion grant");
        async Task<(Guid Id, string Number)> Create()
        {
            var response = await admin.PostAsJsonAsync("/api/employees", input);
            Check(response.StatusCode == HttpStatusCode.Created, "isolated erroneous registration created normally");
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return (json.GetProperty("employeeId").GetGuid(), json.GetProperty("employeeNumber").GetString()!);
        }
        string Path(Guid id) => $"/api/employees/{id}/permanent-deletion";
        async Task<EmployeeDeletionAssessment> Assess(Guid id) => (await admin.GetFromJsonAsync<EmployeeDeletionAssessment>(Path(id)))!;
        async Task<HttpResponseMessage> Delete(Guid id, string number, string? version = null) => await admin.PostAsJsonAsync(Path(id), new { employeeNumber = number, reason = "Disposable erroneous registration", version = version ?? (await Assess(id)).Version });
        var item = await Create();
        Check((await Assess(item.Id)).State == "Eligible", "audited initial registration eligible");
        Check((await anonymous.GetAsync(Path(item.Id))).StatusCode == HttpStatusCode.Unauthorized, "anonymous assessment denied");
        Check((await hr.GetAsync(Path(item.Id))).StatusCode == HttpStatusCode.Forbidden, "Employee.Manage cannot assess deletion");
        Check((await payroll.GetAsync(Path(item.Id))).StatusCode == HttpStatusCode.Forbidden, "PayrollAdmin assessment denied");
        Check((await hr.PostAsJsonAsync(Path(item.Id), new { })).StatusCode == HttpStatusCode.Forbidden, "HRAdmin direct deletion denied");
        admin.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Check((await Delete(item.Id, item.Number)).StatusCode == HttpStatusCode.BadRequest, "missing CSRF rejects permanent deletion");
        var token = await admin.GetFromJsonAsync<JsonElement>("/api/auth/csrf"); admin.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        Check((await Delete(item.Id, "wrong-number")).StatusCode == HttpStatusCode.BadRequest, "exact employee number required");
        var old = await Assess(item.Id);
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
            db.EmployeeContacts.Add(new EmployeeContact { EmployeeId = item.Id, Mobile = "Synthetic-only", IsPrimary = true });
            await db.SaveChangesAsync();
        }
        Check((await Delete(item.Id, item.Number, old.Version)).StatusCode == HttpStatusCode.Conflict, "changed child invalidates stale preview");
        F51ANumberingTests.FailDeletionAudit = true;
        try { Check((await Delete(item.Id, item.Number)).StatusCode == HttpStatusCode.Conflict, "audit failure rolls back deletion"); }
        finally { F51ANumberingTests.FailDeletionAudit = false; }
        F51ANumberingTests.FailDeletionAfterWrite = true;
        try { Check((await Delete(item.Id, item.Number)).StatusCode == HttpStatusCode.Conflict, "failure after SQL writes rolls back deletion"); }
        finally { F51ANumberingTests.FailDeletionAfterWrite = false; }
        Check((await Assess(item.Id)).State == "Eligible", "rollback preserves eligibility and original employee");
        var version = (await Assess(item.Id)).Version;
        var requests = await Task.WhenAll(Delete(item.Id, item.Number, version), Delete(item.Id, item.Number, version));
        Check(requests.Count(x => x.StatusCode == HttpStatusCode.NoContent) == 1, "concurrent duplicate deletion has one winner");
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
            Check(!await db.Employees.AnyAsync(x => x.EmployeeId == item.Id) && !await db.EmploymentRecords.AnyAsync(x => x.EmployeeId == item.Id), "employee and initial employment removed atomically");
            Check(!await db.EmployeeContacts.AnyAsync(x => x.EmployeeId == item.Id), "ordinary contact removed explicitly");
            Check(await db.EmployeeNumberReservations.AnyAsync(x => x.EmployeeId == item.Id && x.RetiredAtUtc != null), "number reservation permanently retired");
            Check(await db.Set<SecurityAuditEvent>().AnyAsync(x => x.ResourceId == item.Id.ToString() && x.Operation.StartsWith("PermanentDeleted;")), "success audit retained without employee FK");
            var identity = new Employee { EmployeeId = item.Id, EmployeeNumber = item.Number, FirstName = "Reuse", LastName = "Forbidden" };
            db.Add(identity);
            bool denied = false; try { await db.SaveChangesAsync(); } catch (DbUpdateException) { denied = true; }
            Check(denied, "retired GUID and number cannot be reinserted");
        }
        var protectedItem = await Create();
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
            var legacy = await db.Employees.SingleAsync(x => x.EmployeeNumber == "TEST-LEGACY");
            Check((await Assess(legacy.EmployeeId)).State != "Eligible", "legacy provenance fails closed");
            db.TeacherProfiles.Add(new TeacherProfile { EmployeeId = protectedItem.Id, TeacherCode = Guid.NewGuid().ToString("N")[..20], TeachingStatus = "Synthetic" });
            await db.SaveChangesAsync();
        }
        Check((await Assess(protectedItem.Id)).Blockers.Contains("protected_records"), "teacher profile protects academic identity");
        Check((await Delete(protectedItem.Id, protectedItem.Number)).StatusCode == HttpStatusCode.Conflict, "protected record cannot be deleted");
        foreach (var kind in new[] { "Attendance", "Leave", "Compensation", "Document", "Photo", "Manager", "ActiveAccount", "DisabledAccount", "EmploymentChange", "RetainedHistory" })
        {
            var candidate = await Create();
            await using (var scope = services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
                switch (kind)
                {
                    case "Attendance": db.Attendance.Add(new EmployeeAttendance { EmployeeId = candidate.Id, AttendanceDate = new(2026, 9, 1), AttendanceStatusId = await db.AttendanceStatuses.Select(x => x.Id).FirstAsync() }); break;
                    case "Leave": db.EmployeeLeaves.Add(new EmployeeLeave { EmployeeId = candidate.Id, StartDate = new(2026, 9, 1), EndDate = new(2026, 9, 1), LeaveTypeId = await db.LeaveTypes.Select(x => x.Id).FirstAsync() }); break;
                    case "Compensation": db.EmployeeCompensations.Add(new EmployeeCompensation { EmployeeId = candidate.Id, PayTypeId = await db.PayTypes.Select(x => x.Id).FirstAsync(), Currency = "THB", IsCurrent = true, EffectiveFrom = new(2026, 9, 1) }); break;
                    case "Document": db.EmployeeDocuments.Add(new EmployeeDocument { EmployeeId = candidate.Id, FileName = "fixture", StorageKey = "fixture" }); break;
                    case "Photo": db.Add(new EmployeePhotoRevision { EmployeeId = candidate.Id, Operation = "Remove", ActorUserId = Guid.NewGuid() }); break;
                    case "Manager": (await db.EmploymentRecords.SingleAsync(x => x.EmployeeId == protectedItem.Id)).ReportingToEmployeeId = candidate.Id; break;
                    case "EmploymentChange": db.Add(new SecurityAuditEvent { ResourceType = "EmploymentRecord", ResourceId = (await db.EmploymentRecords.Where(x => x.EmployeeId == candidate.Id).Select(x => x.EmploymentRecordId).SingleAsync()).ToString(), Operation = $"EmployeeId={candidate.Id};fixture:Modified" }); break;
                    case "RetainedHistory": db.Add(new SecurityAuditEvent { ResourceType = "EmployeeHistory", ResourceId = Guid.NewGuid().ToString(), Operation = $"EmployeeId={candidate.Id};fixture:Deleted" }); break;
                    default:
                        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                        Check((await users.CreateAsync(new ApplicationUser { UserName = "delete-link-" + Guid.NewGuid().ToString("N"), EmployeeId = candidate.Id, IsActive = kind == "ActiveAccount" })).Succeeded, "isolated linked account created"); break;
                }
                await db.SaveChangesAsync();
            }
            var blocked = await Assess(candidate.Id);
            Check(blocked.State != "Eligible", kind + " blocks deletion");
            Check((await Delete(candidate.Id, candidate.Number)).StatusCode == HttpStatusCode.Conflict, kind + " enforced on command recheck");
            if (kind == "Document") Check(blocked.Blockers.Contains("protected_records") && !blocked.Blockers.Any(x => x.Contains("document", StringComparison.OrdinalIgnoreCase)), "SystemAdmin without HRDocuments gets only generic document blocker");
        }
        var targetCandidate = await Create();
        Guid ruleId;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
            var rule = new PayrollRule { Code = Guid.NewGuid().ToString("N"), Name = "Deletion fixture", PayrollComponentId = await db.PayrollComponents.Select(x => x.Id).FirstAsync(), RuleType = "Other", CalculationMethod = "FixedAmount", FixedAmount = 0, AppliesTo = "Employee", EffectiveFrom = new(2026, 9, 1) };
            db.PayrollRules.Add(rule); await db.SaveChangesAsync(); ruleId = rule.PayrollRuleId;
            db.PayrollRuleTargets.Add(new PayrollRuleTarget { PayrollRuleId = ruleId, TargetType = "Employee", TargetId = targetCandidate.Id }); await db.SaveChangesAsync();
        }
        Check((await Assess(targetCandidate.Id)).Blockers.Contains("payroll_reference"), "non-FK payroll target blocks deletion");
        Check((await Delete(targetCandidate.Id, targetCandidate.Number)).StatusCode == HttpStatusCode.Conflict, "non-FK target enforced in transaction");
        var concurrentCandidate = await Create();
        var prior = await Assess(concurrentCandidate.Id);
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await db.Employees.FromSqlInterpolated($"SELECT * FROM Employees WITH (UPDLOCK,HOLDLOCK) WHERE EmployeeId={concurrentCandidate.Id}").SingleAsync();
            var pending = Delete(concurrentCandidate.Id, concurrentCandidate.Number, prior.Version);
            db.Attendance.Add(new EmployeeAttendance { EmployeeId = concurrentCandidate.Id, AttendanceDate = new(2026, 9, 1), AttendanceStatusId = await db.AttendanceStatuses.Select(x => x.Id).FirstAsync() });
            await db.SaveChangesAsync(); await tx.CommitAsync();
            Check((await pending).StatusCode == HttpStatusCode.Conflict, "concurrent dependent creation defeats stale deletion safely");
            Check(await db.Employees.AnyAsync(x => x.EmployeeId == concurrentCandidate.Id), "concurrent dependent and employee preserved");
        }
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.F51DUnknownReference (Id uniqueidentifier PRIMARY KEY, EmployeeId uniqueidentifier NOT NULL REFERENCES dbo.Employees(EmployeeId))");
            try { Check((await Assess(targetCandidate.Id)).Blockers.Contains("schema_requires_review"), "unknown dependency schema fails closed even when empty"); }
            finally { await db.Database.ExecuteSqlRawAsync("DROP TABLE dbo.F51DUnknownReference"); }
        }
        Console.WriteLine($"PASS: {count} F5.1D isolated SQL/HTTP/Identity/CSRF/rollback/deletion assertions.");
    }
}
