using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SIAMIS.Api.Controllers;
using SIAMIS.Api.Security;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Security;
using SIAMIS.Infrastructure.Services;

// Explicit opt-in. Real Identity/cookies/CSRF and SQL Server; isolated database only.
internal static class F72ClockTests
{
    private sealed class Clock : TimeProvider
    {
        public long Ticks = new DateTime(2026, 10, 8, 1, 0, 0, DateTimeKind.Utc).Ticks;
        public override DateTimeOffset GetUtcNow() => new(new DateTime(Interlocked.Add(ref Ticks, 10), DateTimeKind.Utc));
    }
    private sealed class SaveFailure : SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Enabled && data.Context!.ChangeTracker.Entries<AttendanceEvent>().Any(x => x.State == EntityState.Added && x.Entity.Source == "EmployeeClock"))
                throw new DbUpdateException("Isolated injected clock save failure.");
            return ValueTask.FromResult(result);
        }
    }
    public static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool yes, string label) { if (!yes) throw new InvalidOperationException(label); checks++; Console.WriteLine("PASS: " + label); }
        string database = "SIAMIS_F72_Test_" + Guid.NewGuid().ToString("N");
        if (!System.Text.RegularExpressions.Regex.IsMatch(database, "^SIAMIS_F72_Test_[a-f0-9]{32}$")) throw new InvalidOperationException("Unsafe test target.");
        var connection = new SqlConnectionStringBuilder { DataSource = "localhost", InitialCatalog = database, IntegratedSecurity = true, TrustServerCertificate = true };
        var clock = new Clock();
        var saveFailure = new SaveFailure();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", ApplicationName = typeof(F72ClockTests).Assembly.FullName, Args = [] });
        builder.Logging.ClearProviders();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.AddSiamisSecurity();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.RemoveAll<SIAMIS.Application.Security.IHrSecurityReadService>(); // Unused profile/payroll reader in this focused test host.
        builder.Services.AddDbContext<SIAMISDbContext>(o => o.UseSqlServer(connection.ConnectionString).AddInterceptors(saveFailure));
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddScoped<IEmployeeClockService, EmployeeClockService>();
        builder.Services.AddScoped<IAttendanceFoundationService, AttendanceFoundationService>();
        builder.Services.AddScoped<IAttendanceDayService, AttendanceDayService>();
        builder.Services.AddScoped<IAttendanceReviewService, AttendanceReviewService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(EmployeeClockController).Assembly);
        await using var app = builder.Build();
        app.Use(async (context, next) => { try { await next(context); } catch (Exception ex) { Console.WriteLine("F72 isolated request failure: " + ex.GetType().Name); throw; } });
        app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter(); app.MapControllers();
        bool created = false;
        try
        {
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
                created = true; await db.Database.MigrateAsync();
                Check(db.Database.GetDbConnection().Database == database && database != "SIAMIS", "isolated target, never Development SIAMIS");
                var employees = Enumerable.Range(1, 2).Select(n => new Employee { EmployeeNumber = "F72-ISOLATED-" + n, FirstName = "Synthetic", LastName = "Fixture" }).ToArray();
                db.Employees.AddRange(employees);
                var department = await db.Departments.Where(x => x.IsActive).Select(x => x.Id).FirstAsync();
                var designation = await db.Designations.Where(x => x.IsActive).Select(x => x.Id).FirstAsync();
                var type = await db.EmploymentTypes.Where(x => x.IsActive).Select(x => x.Id).FirstAsync();
                var status = await db.EmploymentStatuses.Where(x => x.IsActive && !x.IsTerminal).Select(x => x.Id).FirstAsync();
                foreach (var e in employees) db.EmploymentRecords.Add(new() { EmployeeId = e.EmployeeId, HireDate = new(2026, 9, 1), IsCurrent = true,
                    DepartmentId = department, DesignationId = designation, EmploymentTypeId = type, EmploymentStatusId = status });
                await db.SaveChangesAsync();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                foreach (var pair in new[] { ("clock-one", "Employee", (Guid?)employees[0].EmployeeId), ("clock-two", "Employee", (Guid?)employees[1].EmployeeId), ("clock-admin", "SystemAdmin", (Guid?)null), ("clock-payroll", "PayrollAdmin", (Guid?)null) })
                {
                    var u = new ApplicationUser { UserName = pair.Item1, EmployeeId = pair.Item3, IsActive = true, RequiresPasswordChange = false };
                    // Random ephemeral credential; never returned, logged, stored in files or used for QA personas.
                    var password = Guid.NewGuid().ToString("N");
                    Check((await users.CreateAsync(u, password)).Succeeded, "isolated Identity fixture created");
                    Check((await users.AddToRoleAsync(u, pair.Item2)).Succeeded, "existing role assigned in isolated fixture");
                    passwords[pair.Item1] = password;
                }
            }
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            HttpClient Client() => new(new HttpClientHandler { CookieContainer = new(), AllowAutoRedirect = false }) { BaseAddress = new(address) };
            async Task Csrf(HttpClient c)
            {
                var result = await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
                c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", result.GetProperty("token").GetString());
            }
            async Task<HttpClient> Login(string name)
            {
                var c = Client(); await Csrf(c);
                Check((await c.PostAsJsonAsync("/api/auth/login", new { userName = name, password = passwords[name] })).StatusCode == HttpStatusCode.NoContent, "real Identity login " + name);
                await Csrf(c); return c;
            }
            async Task<JsonElement> Json(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();
            using var anonymous = Client();
            Check((await anonymous.GetAsync("/api/self/attendance/current")).StatusCode == HttpStatusCode.Unauthorized, "anonymous clock reads denied");
            Check((await anonymous.PostAsJsonAsync("/api/self/attendance/clock-in", new { requestKey = Guid.NewGuid(), workArrangement = "OnCampus" })).StatusCode == HttpStatusCode.Unauthorized, "anonymous clock commands denied");
            using var first = await Login("clock-one"); using var second = await Login("clock-two");
            using var admin = await Login("clock-admin"); using var payroll = await Login("clock-payroll");
            foreach (var c in new[] { admin, payroll }) Check((await c.GetAsync("/api/self/attendance/current")).StatusCode == HttpStatusCode.Forbidden, "non-SelfService role has no clock access");
            first.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            Check((await first.PostAsJsonAsync("/api/self/attendance/clock-in", new { requestKey = Guid.NewGuid(), workArrangement = "OnCampus" })).StatusCode == HttpStatusCode.BadRequest, "real missing CSRF denied"); await Csrf(first);
            foreach (var body in new object[] { new { requestKey = Guid.Empty, workArrangement = "OnCampus" }, new { requestKey = Guid.NewGuid(), workArrangement = "Invalid" }, new { requestKey = Guid.NewGuid(), workArrangement = 0 }, new { requestKey = Guid.NewGuid(), workArrangement = "OnCampus", employeeId = Guid.NewGuid() }, new { requestKey = Guid.NewGuid(), workArrangement = "OnCampus", occurredAt = "2026-10-08T01:00:00Z" } })
                Check((await first.PostAsJsonAsync("/api/self/attendance/clock-in", body)).StatusCode == HttpStatusCode.BadRequest, "invalid/forged clock fields rejected");
            var key = Guid.NewGuid(); var input = new { requestKey = key, workArrangement = "OnlineClass" };
            var simultaneous = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => first.PostAsJsonAsync("/api/self/attendance/clock-in", input)));
            Console.WriteLine("Concurrent IN HTTP statuses: " + string.Join(", ", simultaneous.Select(r => (int)r.StatusCode)));
            Check(simultaneous.Count(r => r.StatusCode == HttpStatusCode.Created) == 1 && simultaneous.All(r => r.IsSuccessStatusCode), "concurrent same-key IN creates exactly once, others replay");
            var opened = (await Json(simultaneous.First(r => r.StatusCode == HttpStatusCode.Created))).GetProperty("session");
            Guid session = opened.GetProperty("sessionId").GetGuid();
            Check(opened.GetProperty("workArrangement").GetString() == "OnlineClass" && opened.GetProperty("businessTimeZone").GetString() == "Asia/Bangkok", "arrangement and Bangkok metadata persisted");
            Check(opened.GetProperty("clockedInAtUtc").GetString()!.EndsWith('Z'), "authoritative timestamp serialized UTC");
            Check((await first.PostAsJsonAsync("/api/self/attendance/clock-in", new { requestKey = Guid.NewGuid(), workArrangement = "RemoteWork" })).StatusCode == HttpStatusCode.Conflict, "second open session rejected");
            Check((await first.PostAsJsonAsync("/api/self/attendance/clock-in", new { requestKey = key, workArrangement = "RemoteWork" })).StatusCode == HttpStatusCode.Conflict, "key payload drift rejected");
            Check((await second.PostAsJsonAsync("/api/self/attendance/clock-out", new { requestKey = Guid.NewGuid(), sessionId = session })).StatusCode == HttpStatusCode.NotFound, "cross-employee OUT rejected");
            Check((await second.GetStringAsync("/api/self/attendance/current")) == "null", "other employee sees no open session");
            var outKey = Guid.NewGuid(); var output = new { requestKey = outKey, sessionId = session };
            var closed = await first.PostAsJsonAsync("/api/self/attendance/clock-out", output); Check(closed.StatusCode == HttpStatusCode.Created, "owned OUT succeeds");
            Check((await first.PostAsJsonAsync("/api/self/attendance/clock-out", output)).StatusCode == HttpStatusCode.OK, "OUT replay succeeds without new timestamp");
            Check((await first.PostAsJsonAsync("/api/self/attendance/clock-out", new { requestKey = Guid.NewGuid(), sessionId = session })).StatusCode == HttpStatusCode.NotFound, "closed session cannot be closed twice");
            foreach (var mode in new[] { "OnCampus", "RemoteWork" })
            {
                var r = await first.PostAsJsonAsync("/api/self/attendance/clock-in", new { requestKey = Guid.NewGuid(), workArrangement = mode });
                Check(r.StatusCode == HttpStatusCode.Created, "multiple daily sessions " + mode);
                var id = (await Json(r)).GetProperty("session").GetProperty("sessionId").GetGuid();
                Check((await first.PostAsJsonAsync("/api/self/attendance/clock-out", new { requestKey = Guid.NewGuid(), sessionId = id })).StatusCode == HttpStatusCode.Created, "complete daily session " + mode);
            }
            var history = await first.GetFromJsonAsync<JsonElement>("/api/self/attendance/sessions?from=2026-10-08&to=2026-10-08&pageSize=2");
            Check(history.GetProperty("totalCount").GetInt32() == 3 && history.GetProperty("items").GetArrayLength() == 2, "owned history paged at database");
            Check((await second.GetFromJsonAsync<JsonElement>("/api/self/attendance/sessions")).GetProperty("totalCount").GetInt32() == 0, "owned history excludes another employee");
            Check((await first.GetAsync("/api/self/attendance/sessions?pageSize=101")).StatusCode == HttpStatusCode.BadRequest, "history page bound enforced");
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
                Check(await db.AttendanceEvents.CountAsync() == 6 && await db.EmployeeClockSessions.CountAsync() == 3, "only six immutable events and three sessions created");
                Check(await db.EmployeePayrolls.CountAsync() == 0 && await db.EmployeeLeaves.CountAsync() == 0, "no payroll or Leave side effects");
                var eventRow = await db.AttendanceEvents.FirstAsync();
                Check(eventRow.ActorId.HasValue && eventRow.Source == "EmployeeClock" && eventRow.OccurredAtUtc == eventRow.ReceivedAtUtc && eventRow.OriginalSourceTimestamp is null, "audited server evidence provenance");
                Check(await db.Set<SecurityAuditEvent>().AnyAsync(x => x.ResourceType == nameof(EmployeeClockSession)), "existing atomic audit captures sessions");
                eventRow.Direction = "Out";
                try { await db.SaveChangesAsync(); Check(false, "immutable event mutation blocked"); } catch (InvalidOperationException) { Check(true, "immutable event mutation blocked"); }
                db.ChangeTracker.Clear(); var oldSession = await db.EmployeeClockSessions.FirstAsync(); oldSession.OutEventId = null;
                try { await db.SaveChangesAsync(); Check(false, "closed provenance cannot reopen"); } catch (InvalidOperationException) { Check(true, "closed provenance cannot reopen"); }
                db.ChangeTracker.Clear();
            }
            var competing = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => first.PostAsJsonAsync("/api/self/attendance/clock-in", new { requestKey = Guid.NewGuid(), workArrangement = "RemoteWork" })));
            Check(competing.Count(r => r.StatusCode == HttpStatusCode.Created) == 1 && competing.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1, "different-key concurrent IN permits only one open session");
            var competingId = (await Json(competing.Single(r => r.StatusCode == HttpStatusCode.Created))).GetProperty("session").GetProperty("sessionId").GetGuid();
            var closeKey = Guid.NewGuid();
            var closes = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => first.PostAsJsonAsync("/api/self/attendance/clock-out", new { requestKey = closeKey, sessionId = competingId })));
            Check(closes.Count(r => r.StatusCode == HttpStatusCode.Created) == 1 && closes.All(r => r.IsSuccessStatusCode), "concurrent same-key OUT closes once and replays");
            Check((await first.PostAsJsonAsync("/api/self/attendance/clock-in", new { requestKey = closeKey, workArrangement = "RemoteWork" })).StatusCode == HttpStatusCode.Conflict, "request key cannot switch command direction");
            var failingKey = Guid.NewGuid(); int auditBefore;
            using (var scope = app.Services.CreateScope()) auditBefore = await scope.ServiceProvider.GetRequiredService<SIAMISDbContext>().Set<SecurityAuditEvent>().CountAsync();
            saveFailure.Enabled = true;
            Check((await first.PostAsJsonAsync("/api/self/attendance/clock-in", new { requestKey = failingKey, workArrangement = "OnCampus" })).StatusCode == HttpStatusCode.InternalServerError, "injected save failure returns no successful clock response");
            saveFailure.Enabled = false;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
                Check(await db.AttendanceEvents.CountAsync() == 8 && await db.EmployeeClockSessions.CountAsync() == 4 && await db.Set<SecurityAuditEvent>().CountAsync() == auditBefore,
                    "failed clock save rolls back event, session and audit together");
            }
            var recovered = await first.PostAsJsonAsync("/api/self/attendance/clock-in", new { requestKey = failingKey, workArrangement = "OnCampus" });
            Check(recovered.StatusCode == HttpStatusCode.Created, "failed uncommitted key can be retried safely");
            var recoveredId = (await Json(recovered)).GetProperty("session").GetProperty("sessionId").GetGuid();
            Check((await first.PostAsJsonAsync("/api/self/attendance/clock-out", new { requestKey = Guid.NewGuid(), sessionId = recoveredId })).StatusCode == HttpStatusCode.Created, "retry result closes normally");
            Guid employeeId;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
                employeeId = await db.Employees.Where(x => x.EmployeeNumber == "F72-ISOLATED-1").Select(x => x.EmployeeId).SingleAsync();
                var calendar = new SIAMIS.Domain.Entities.Leave.WorkCalendar { Code = "F72-ISOLATED", Name = "Isolated clock schedule", IsActive = true };
                db.WorkCalendars.Add(calendar);
                db.Add(new SIAMIS.Domain.Entities.Leave.EmployeeWorkCalendarAssignment { EmployeeId = employeeId, WorkCalendarId = calendar.Id, EffectiveFrom = new(2026, 9, 1) });
                db.Add(new SIAMIS.Domain.Entities.Leave.WorkCalendarWeeklyInterval { WorkCalendarId = calendar.Id, DayOfWeek = DayOfWeek.Thursday, StartTime = new(8, 0), EndTime = new(9, 0) });
                await db.SaveChangesAsync();
            }
            string reviewPath = $"/api/employees/{employeeId}/attendance-days/2026-10-08";
            Check((await first.GetAsync(reviewPath + "/review")).StatusCode == HttpStatusCode.Forbidden, "SelfService cannot enter HR review");
            var review = await admin.GetFromJsonAsync<JsonElement>(reviewPath + "/review");
            Check(review.GetProperty("calculation").GetProperty("coveragePartitionAvailable").GetBoolean(), "clock evidence integrates with existing tick partition");
            object Decision(JsonElement r) => new { expectedVersion = r.GetProperty("version").GetInt64(), expectedSourceFingerprint = r.GetProperty("sourceFingerprint").GetString(), reason = "Isolated clocking regression decision" };
            Check((await admin.PostAsJsonAsync(reviewPath + "/finalize", Decision(review))).StatusCode == HttpStatusCode.Created, "existing HR finalization accepts complete clock evidence");
            string frozen;
            using (var scope = app.Services.CreateScope()) frozen = await scope.ServiceProvider.GetRequiredService<SIAMISDbContext>().Set<FinalizedAttendanceRevision>().Select(x => x.SnapshotJson).SingleAsync();
            var unclosed = await first.PostAsJsonAsync("/api/self/attendance/clock-in", new { requestKey = Guid.NewGuid(), workArrangement = "OnCampus" });
            Check(unclosed.StatusCode == HttpStatusCode.Created, "new clock evidence can append after historical finalization");
            var unclosedId = (await Json(unclosed)).GetProperty("session").GetProperty("sessionId").GetGuid();
            var stale = await admin.GetFromJsonAsync<JsonElement>(reviewPath + "/review");
            Check(stale.GetProperty("isStale").GetBoolean() && stale.GetProperty("requiresReopen").GetBoolean()
                && !stale.GetProperty("isCurrentlyValidated").GetBoolean(), "new clock evidence makes frozen day stale without auto-reopen");
            Check(stale.GetProperty("rawCalculation").GetProperty("findings").EnumerateArray().Any(x => x.GetProperty("code").GetString() == "MissingClockOut"), "missing OUT remains review finding");
            Check((await admin.PostAsJsonAsync(reviewPath + "/reopen", Decision(stale))).StatusCode == HttpStatusCode.OK, "existing explicit HR reopen remains supported");
            var reopened = await admin.GetFromJsonAsync<JsonElement>(reviewPath + "/review");
            Check((await admin.PostAsJsonAsync(reviewPath + "/corrections", new { expectedVersion = reopened.GetProperty("version").GetInt64(), expectedSourceFingerprint = reopened.GetProperty("sourceFingerprint").GetString(), reason = "Isolated missing OUT correction", occurredAt = "2026-10-08T08:30:00+07:00", direction = "Out", manualRequestKey = Guid.NewGuid() })).StatusCode == HttpStatusCode.Created, "existing correction appends separate evidence");
            var savedTicks = clock.Ticks; clock.Ticks -= TimeSpan.TicksPerDay;
            Check((await first.PostAsJsonAsync("/api/self/attendance/clock-out", new { requestKey = Guid.NewGuid(), sessionId = unclosedId })).StatusCode == HttpStatusCode.Conflict, "server clock regression rejected without fabricated timestamp");
            clock.Ticks = savedTicks + TimeSpan.TicksPerDay;
            using var nextDayEmployee = await Login("clock-one"); // Normal eight-hour session expiry is preserved across the test's one-day jump.
            var nextDay = await nextDayEmployee.PostAsJsonAsync("/api/self/attendance/clock-out", new { requestKey = Guid.NewGuid(), sessionId = unclosedId });
            Check(nextDay.StatusCode == HttpStatusCode.Created && (await Json(nextDay)).GetProperty("session").GetProperty("outBusinessDate").GetString() == "2026-10-09", "cross-midnight session closes with actual next-day timestamp");
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
                Check(await db.Set<FinalizedAttendanceRevision>().Select(x => x.SnapshotJson).SingleAsync() == frozen, "frozen historical snapshot remains byte-for-byte unchanged");
                Check(await db.EmployeePayrolls.CountAsync() == 0 && await db.EmployeePayrollLines.CountAsync() == 0 && await db.EmployeeLeaves.CountAsync() == 0, "review/correction/clocking creates no payroll or Leave data");
                var e = await db.Employees.SingleAsync(x => x.EmployeeId == employeeId); e.IsActive = false; await db.SaveChangesAsync();
            }
            Check((await nextDayEmployee.PostAsJsonAsync("/api/self/attendance/clock-in", new { requestKey = Guid.NewGuid(), workArrangement = "OnCampus" })).StatusCode == HttpStatusCode.BadRequest, "inactive employee cannot clock");
            Console.WriteLine($"PASS: {checks} F7.2 real HTTP/Identity/CSRF/SQL assertions.");
        }
        finally
        {
            passwords.Clear(); await app.StopAsync(); SqlConnection.ClearAllPools();
            // Target was generated and validated above; never accepts an operator database name.
            if (created)
            {
                var master = new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = "master" };
                await using var sql = new SqlConnection(master.ConnectionString); await sql.OpenAsync();
                await using var drop = new SqlCommand($"IF DB_ID(N'{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END", sql); await drop.ExecuteNonQueryAsync();
                Console.WriteLine("Isolated F7.2 database removed; Development SIAMIS was not targeted.");
            }
        }
    }
    private static readonly Dictionary<string, string> passwords = new();
}
