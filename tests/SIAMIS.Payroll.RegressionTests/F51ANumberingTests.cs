using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SIAMIS.Api.Controllers;
using SIAMIS.Api.Security;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Security;
using SIAMIS.Infrastructure.Services;

// Real SQL/Identity/CSRF, explicit opt-in, generated disposable database only.
internal static class F51ANumberingTests
{
    internal static bool FailPhotoAfterWrite;
    internal static bool FailDeletionAudit;
    internal static bool FailDeletionAfterWrite;
    internal static bool FailRegistrationReceipt;
    private sealed class RegistrationKeys : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/employees" && !request.Headers.Contains("Idempotency-Key"))
                request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
            return base.SendAsync(request, ct);
        }
    }
    private sealed class Failure : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (FailRegistrationReceipt && data.Context!.ChangeTracker.Entries<EmployeeRegistrationReceipt>().Any(x => x.State == EntityState.Added))
                throw new DbUpdateException("Isolated receipt persistence failure.");
            if (FailDeletionAudit && data.Context!.ChangeTracker.Entries<SecurityAuditEvent>().Any(x => x.Entity.Operation.StartsWith("PermanentDeleted;")))
                throw new DbUpdateException("Isolated deletion audit failure.");
            if (data.Context!.ChangeTracker.Entries<Employee>().Any(e => e.State == EntityState.Added && e.Entity.FirstName == "FailSave"))
                throw new DbUpdateException("Isolated registration save failure.");
            return base.SavingChangesAsync(data, result, ct);
        }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
        {
            if (FailDeletionAfterWrite && data.Context!.ChangeTracker.Entries<SecurityAuditEvent>().Any(x => x.Entity.Operation.StartsWith("PermanentDeleted;")))
                throw new DbUpdateException("Isolated deletion failure after SQL writes.");
            if (FailPhotoAfterWrite && data.Context!.ChangeTracker.Entries<EmployeePhotoRevision>().Any(e => e.Entity.IsCurrent))
                throw new DbUpdateException("Isolated photo failure after SQL writes.");
            if (data.Context!.ChangeTracker.Entries<Employee>().Any(e => e.Entity.FirstName == "FailAfterSave"))
                throw new DbUpdateException("Isolated failure after SQL writes, before registration commit.");
            return base.SavedChangesAsync(data, result, ct);
        }
    }
    public static async Task RunAsync(bool includeSupporting = false, bool includePhotos = false, bool includeDeletion = false, bool includeAcceptance = false, bool includeIdempotency = false)
    {
        int checks = 0;
        void Check(bool pass, string label) { if (!pass) throw new InvalidOperationException("FAIL: " + label); checks++; Console.WriteLine("PASS: " + label); }
        string database = "SIAMIS_F51A_Test_" + Guid.NewGuid().ToString("N");
        if (!System.Text.RegularExpressions.Regex.IsMatch(database, "^SIAMIS_F51A_Test_[a-f0-9]{32}$")) throw new InvalidOperationException("Unsafe test database.");
        var connection = new SqlConnectionStringBuilder { DataSource = "localhost", InitialCatalog = database, IntegratedSecurity = true, TrustServerCertificate = true };
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", Args = [] });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Security:EnableDevelopmentCredentialDelivery"] = "true" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.AddSiamisSecurity();
        builder.Services.Configure<SIAMIS.Infrastructure.Documents.PrivateDocumentOptions>(_ => { });
        builder.Services.AddSingleton<IPrivateDocumentStorage, SIAMIS.Infrastructure.Documents.PrivateDocumentStorage>();
        builder.Services.RemoveAll<SIAMIS.Application.Security.IHrSecurityReadService>(); // Unused payroll/self-profile reader in this isolated host.
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddDbContext<SIAMISDbContext>(o => o.UseSqlServer(connection.ConnectionString).AddInterceptors(new Failure()));
        builder.Services.AddScoped<IEmployeeService, EmployeeService>();
        builder.Services.AddScoped<IEmployeeDeletionService, EmployeeDeletionService>();
        builder.Services.AddScoped<IEmployeeContactsService, EmployeeContactsService>();
        builder.Services.AddScoped<IEmployeeAddressesService, EmployeeAddressesService>();
        builder.Services.AddScoped<IEmployeeEmergencyContactsService, EmployeeEmergencyContactsService>();
        var photoStorage = new F51CPhotoTests.FaultStorage();
        builder.Services.AddSingleton<IEmployeePhotoStorage>(photoStorage);
        builder.Services.AddScoped<IEmployeePhotoService, SIAMIS.Infrastructure.Documents.EmployeePhotoService>();
        builder.Services.AddScoped<IEmploymentLifecycleService, EmploymentLifecycleService>();
        builder.Services.AddScoped<IEmploymentResolver, EmploymentLifecycleService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(EmployeesController).Assembly);
        await using var app = builder.Build();
        app.UseExceptionHandler(e => e.Run(c => {
            var failure = c.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
            Console.WriteLine($"Isolated HTTP failure: {failure?.GetType().Name}: {failure?.GetBaseException().Message}");
            Console.WriteLine(failure?.StackTrace);
            c.Response.StatusCode = 500; return Task.CompletedTask;
        }));
        app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter(); app.MapControllers();
        var passwords = new Dictionary<string, string>();
        bool created = false;
        try
        {
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
                created = true;
                await db.GetService<IMigrator>().MigrateAsync("20261008061002_AddEmployeeClockingFoundation");
                var legacy = new Employee { EmployeeNumber = "TEST-LEGACY", FirstName = "Legacy", LastName = "Fixture", IsActive = false };
                // Pre-migration schema: explicitly disable OUTPUT just as the new model requires.
                db.Employees.Add(legacy); await db.SaveChangesAsync();
                await db.Database.MigrateAsync();
                Check(await db.Employees.AnyAsync(x => x.EmployeeId == legacy.EmployeeId && x.EmployeeNumber == "TEST-LEGACY" && !x.IsActive), "legacy GUID, number and inactive state preserved by migration");
                Check(await db.EmployeeNumberReservations.AnyAsync(x => x.EmployeeId == legacy.EmployeeId && x.EmployeeNumber == "TEST-LEGACY" && x.AssignedAtUtc != null), "legacy identity permanently reserved");
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                foreach (var pair in new[] { ("number-admin", "SystemAdmin"), ("number-hr", "HRAdmin"), ("number-payroll", "PayrollAdmin") })
                {
                    var user = new ApplicationUser { UserName = pair.Item1, IsActive = true, RequiresPasswordChange = false };
                    string password = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                    Check((await users.CreateAsync(user, password)).Succeeded && (await users.AddToRoleAsync(user, pair.Item2)).Succeeded, "isolated Identity fixture " + pair.Item2);
                    passwords[pair.Item1] = password;
                }
            }
            await app.StartAsync();
            string url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            async Task Csrf(HttpClient client)
            {
                client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
                var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
                client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
            }
            async Task<HttpClient> Login(string name)
            {
                var client = new HttpClient(new RegistrationKeys { InnerHandler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false } }) { BaseAddress = new Uri(url) };
                await Csrf(client);
                Check((await client.PostAsJsonAsync("/api/auth/login", new { userName = name, password = passwords[name] })).IsSuccessStatusCode, "real Identity login " + name);
                await Csrf(client); return client;
            }
            Dictionary<string, object?> input;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
                input = new() { ["firstName"] = "Synthetic", ["lastName"] = "Numbering", ["hireDate"] = "2026-09-01",
                    ["departmentId"] = await db.Departments.Where(x => x.IsActive).Select(x => x.Id).FirstAsync(),
                    ["designationId"] = await db.Designations.Where(x => x.IsActive).Select(x => x.Id).FirstAsync(),
                    ["employmentTypeId"] = await db.EmploymentTypes.Where(x => x.IsActive).Select(x => x.Id).FirstAsync(),
                };
            }
            using var admin = await Login("number-admin"); using var hr = await Login("number-hr"); using var payroll = await Login("number-payroll");
            using var anonymous = new HttpClient { BaseAddress = new Uri(url) };
            Check((await anonymous.PostAsJsonAsync("/api/employees", input)).StatusCode == HttpStatusCode.Unauthorized, "anonymous registration denied");
            Check((await payroll.PostAsJsonAsync("/api/employees", input)).StatusCode == HttpStatusCode.Forbidden, "PayrollAdmin cannot register employees");
            admin.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            var noCsrf = await admin.PostAsJsonAsync("/api/employees", input);
            Check(noCsrf.StatusCode == HttpStatusCode.BadRequest, "missing CSRF rejects registration"); await Csrf(admin);
            var first = await admin.PostAsJsonAsync("/api/employees", input);
            Check(first.StatusCode == HttpStatusCode.Created, "registration creates employee and employment");
            var employee = await first.Content.ReadFromJsonAsync<JsonElement>();
            Guid id = employee.GetProperty("employeeId").GetGuid();
            Check(employee.GetProperty("employeeNumber").GetString() == "100000", "first allocation is 100000");
            foreach (object? attempt in new object?[] { "100001", "100000", null })
            {
                var forged = new Dictionary<string, object?>(input) { ["employeeNumber"] = attempt };
                Check((await admin.PostAsJsonAsync("/api/employees", forged)).StatusCode == HttpStatusCode.BadRequest, "explicit create number rejected, including null");
                Check((await admin.PutAsJsonAsync($"/api/employees/{id}", forged)).StatusCode == HttpStatusCode.BadRequest, "renumbering field rejected, including unchanged/null");
            }
            var updateInput = new Dictionary<string, object?>(input) { ["employmentStatusId"] = employee.GetProperty("currentEmployment").GetProperty("employmentStatusId").GetGuid() };
            Check((await hr.PutAsJsonAsync($"/api/employees/{id}", updateInput)).IsSuccessStatusCode, "HR profile update without number remains supported");
            var concurrent = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => hr.PostAsJsonAsync("/api/employees", input)));
            Check(concurrent.All(x => x.StatusCode == HttpStatusCode.Created), "concurrent registrations all succeed");
            var values = new List<string>(); foreach (var r in concurrent) values.Add((await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("employeeNumber").GetString()!);
            Check(values.Distinct().Count() == 6 && values.Order().SequenceEqual(Enumerable.Range(100001, 6).Select(x => x.ToString())), "concurrent numbers are unique and increasing");
            var failure = new Dictionary<string, object?>(input) { ["firstName"] = "FailSave" };
            Check((await admin.PostAsJsonAsync("/api/employees", failure)).StatusCode == HttpStatusCode.InternalServerError, "injected save failure is not success");
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
                Check(!await db.Employees.AnyAsync(x => x.FirstName == "FailSave") && await db.EmploymentRecords.CountAsync() == 7, "failed registration leaves no partial employee or employment");
                Check(await db.EmployeeNumberReservations.AnyAsync(x => x.EmployeeNumber == "100007" && x.AssignedAtUtc == null), "failed allocation retains permanent reservation");
            }
            var next = await admin.PostAsJsonAsync("/api/employees", input);
            Check((await next.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("employeeNumber").GetString() == "100008", "failed number is never automatically reused");
            async Task Rejected(string sql, string label)
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
                bool rejected = false; try { await db.Database.ExecuteSqlRawAsync(sql); } catch (SqlException e) when (e.Number is 51002 or 51003 or 51004 or 51005 or 547 or 2601 or 2627) { rejected = true; }
                Check(rejected, label);
            }
            await Rejected($"UPDATE Employees SET EmployeeNumber='RENUMBERED' WHERE EmployeeId='{id}'", "direct SQL renumbering rejected");
            await Rejected($"UPDATE Employees SET EmployeeId=NEWID() WHERE EmployeeId='{id}'", "direct SQL GUID replacement rejected");
            // Existing lifecycle and separate account provisioning, only in the disposable host.
            Check((await admin.PostAsJsonAsync($"/api/employees/{id}/employment-changes", new { effectiveDate = "2026-09-15", departmentId = input["departmentId"] })).StatusCode == HttpStatusCode.Created, "employment context change retains identity");
            Check((await hr.PostAsJsonAsync("/api/admin/users", new { userName = "isolated-onboarding", email = "isolated@example.invalid", employeeId = id, roles = new[] { "Employee" } })).StatusCode == HttpStatusCode.Forbidden, "HR registration does not grant account provisioning");
            var account = await admin.PostAsJsonAsync("/api/admin/users", new { userName = "isolated-onboarding", email = "isolated@example.invalid", employeeId = id, roles = new[] { "Employee" } });
            Check(account.StatusCode == HttpStatusCode.Created, "separate D13 passwordless provisioning remains supported");
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
                var user = await db.Users.SingleAsync(u => u.UserName == "isolated-onboarding");
                Check(user.PasswordHash == null && !user.EmailConfirmed && user.EmployeeId == id, "provisioning preserves activation and Employee linkage");
                Check(await db.Set<SecurityAuditEvent>().AnyAsync(x => x.ResourceType == "Employee" && x.ResourceId == id.ToString() && x.ActorUserId != null), "authenticated employee mutation audit preserved");
                // Retire a different unlinked fixture; no production deletion UI is introduced.
                var disposable = await db.Employees.SingleAsync(x => x.EmployeeNumber == "100008");
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM EmploymentRecords WHERE EmployeeId={disposable.EmployeeId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Employees WHERE EmployeeId={disposable.EmployeeId}");
                Check(await db.EmployeeNumberReservations.AnyAsync(x => x.EmployeeNumber == "100008" && x.RetiredAtUtc != null), "deletion permanently retires the number");
            }
            string Insert(string number, Guid guid) => $"INSERT Employees (EmployeeId,EmployeeNumber,FirstName,LastName,IsActive,CreatedAt,UpdatedAt) VALUES ('{guid}','{number}','Synthetic','Reuse',1,SYSUTCDATETIME(),SYSUTCDATETIME())";
            await Rejected(Insert("100008", Guid.NewGuid()), "explicit insertion of retired number rejected");
            await Rejected(Insert("100007", Guid.NewGuid()), "failed reservation cannot be reassigned to another GUID");
            await Rejected(Insert("100100", Guid.NewGuid()), "unreserved explicit insertion rejected");
            await Rejected("DELETE EmployeeNumberReservations WHERE EmployeeNumber='100008'", "retired reservation cannot be deleted");
            await Rejected("UPDATE EmployeeNumberReservations SET EmployeeId=NEWID() WHERE EmployeeNumber='100008'", "retired reservation cannot be rebound");
            await Rejected("UPDATE Employees SET EmployeeNumber='100008' WHERE EmployeeNumber='100001'", "retired number cannot be reassigned to a live employee");
            Check((await hr.GetFromJsonAsync<JsonElement>($"/api/employees/{id}")).GetProperty("employeeNumber").GetString() == "100000", "profile and lifecycle preserve permanent number");
            var afterSaveFailure = new Dictionary<string, object?>(input) { ["firstName"] = "FailAfterSave" };
            Check((await admin.PostAsJsonAsync("/api/employees", afterSaveFailure)).StatusCode == HttpStatusCode.InternalServerError, "failure after SQL writes is not successful registration");
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
                Check(!await db.Employees.AnyAsync(x => x.FirstName == "FailAfterSave") && await db.EmploymentRecords.CountAsync() == 8,
                    "post-write failure rolls back employee and initial employment together");
                Check(await db.EmployeeNumberReservations.AnyAsync(x => x.EmployeeNumber == "100009" && x.AssignedAtUtc == null), "post-write rollback preserves only independent number reservation");
                bool refused = false;
                try { await db.GetService<IMigrator>().MigrateAsync("20261008061002_AddEmployeeClockingFoundation"); }
                catch (SqlException e) when (e.Number is 51006 or 51031) { refused = true; }
                Check(refused && await db.EmployeeNumberReservations.AnyAsync(x => x.EmployeeNumber == "100008" && x.RetiredAtUtc != null), "automatic downgrade refuses to erase permanent reservations");
                // Either permanent receipt or numbering protection must refuse destructive downgrade.
                // Restore the disposable fixture to the current model before subsequent contract checks.
                await db.Database.MigrateAsync();
            }
            await PreflightAsync(Check);
            if (includeSupporting)
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
                var other = await db.Employees.Where(x => x.EmployeeNumber == "100001").Select(x => x.EmployeeId).SingleAsync();
                var addressType = await db.AddressTypes.Where(x => x.IsActive).Select(x => x.Id).FirstAsync();
                await F51BSupportingTests.RunAsync(admin, hr, payroll, anonymous, id, other, addressType);
                if (includePhotos)
                {
                    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var self = new ApplicationUser { UserName = "photo-self", EmployeeId = other, IsActive = true, RequiresPasswordChange = false };
                    var secret = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                    Check((await users.CreateAsync(self, secret)).Succeeded && (await users.AddToRoleAsync(self, "Employee")).Succeeded, "isolated linked employee persona");
                    passwords["photo-self"] = secret;
                    using var selfClient = await Login("photo-self");
                    await F51CPhotoTests.RunAsync(admin, hr, payroll, anonymous, selfClient, id, other, photoStorage, db);
                }
                foreach (var name in new[] { "Suspended", "On Leave", "Inactive", "Active" })
                {
                    var suppliedStatus = await db.EmploymentStatuses.Where(x => x.Name == name).Select(x => x.Id).SingleAsync();
                    var forgedStatus = new Dictionary<string, object?>(input) { ["employmentStatusId"] = suppliedStatus };
                    Check((await hr.PostAsJsonAsync("/api/employees", forgedStatus)).StatusCode == HttpStatusCode.BadRequest,
                        "registration rejects caller-supplied " + name + " status");
                }
                var futureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
                var futureInput = new Dictionary<string, object?>(input) { ["hireDate"] = futureDate.ToString("yyyy-MM-dd") };
                var futureResponse = await hr.PostAsJsonAsync("/api/employees", futureInput);
                Check(futureResponse.StatusCode == HttpStatusCode.Created, "future registration without status succeeds");
                var futureEmployee = await futureResponse.Content.ReadFromJsonAsync<JsonElement>();
                var futureId = futureEmployee.GetProperty("employeeId").GetGuid();
                Check(futureEmployee.GetProperty("currentEmployment").GetProperty("employmentStatus").GetString() == "Active",
                    "server selects initial Active status");
                var resolver = scope.ServiceProvider.GetRequiredService<IEmploymentResolver>();
                Check((await resolver.ResolveAsync(futureId, futureDate.AddDays(-1), default)).Value is null,
                    "future hire has no employment coverage before hire date");
                Check((await resolver.ResolveAsync(futureId, futureDate, default)).Value is not null,
                    "initial employment becomes effective on hire date");
                var laterStart = futureDate.AddDays(3);
                futureInput["startDate"] = laterStart.ToString("yyyy-MM-dd");
                var laterResponse = await hr.PostAsJsonAsync("/api/employees", futureInput);
                var laterId = (await laterResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("employeeId").GetGuid();
                Check((await resolver.ResolveAsync(laterId, futureDate, default)).Value is null &&
                    (await resolver.ResolveAsync(laterId, laterStart, default)).Value is not null,
                    "explicit later start preserves effective-date behavior");
                var userCount = await db.Users.CountAsync();
                var registration = new Dictionary<string, object?>(input) {
                    ["contacts"] = new[] { new { workEmail = "registration@example.invalid", mobile = "0100000000", isPrimary = true } }
                };
                var registered = await hr.PostAsJsonAsync("/api/employees", registration);
                Check(registered.StatusCode == HttpStatusCode.Created, "F5.1B registration with initial contact succeeds");
                var profile = await registered.Content.ReadFromJsonAsync<JsonElement>();
                var registeredId = profile.GetProperty("employeeId").GetGuid();
                Check(profile.GetProperty("contacts").GetArrayLength() == 1 &&
                    await db.EmploymentRecords.CountAsync(x => x.EmployeeId == registeredId) == 1 &&
                    await db.Users.CountAsync() == userCount,
                    "F5.1B initial contact and employment persisted together without account provisioning");
            }
            if (includeDeletion) await F51DDeletionTests.RunAsync(admin, hr, payroll, anonymous, input, app.Services);
            if (includeAcceptance) await F51EAcceptanceTests.RunAsync(admin, hr, payroll, input, app.Services);
            if (includeIdempotency) await F51E1IdempotencyTests.RunAsync(app.Services, admin, hr, payroll, anonymous, input, passwords["number-admin"]);
            Console.WriteLine($"PASS: {checks} F5.1A SQL/HTTP/Identity/CSRF assertions.");
        }
        finally
        {
            photoStorage.Dispose(); FailPhotoAfterWrite = false;
            passwords.Clear(); await app.StopAsync(); SqlConnection.ClearAllPools();
            if (created)
            {
                var master = new SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog = "master" };
                await using var sql = new SqlConnection(master.ConnectionString); await sql.OpenAsync();
                await using var drop = new SqlCommand($"IF DB_ID(N'{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END", sql);
                await drop.ExecuteNonQueryAsync(); Console.WriteLine("Disposable F5.1A database removed; Development SIAMIS untouched.");
            }
        }
    }

    private static async Task PreflightAsync(Action<bool, string> check)
    {
        // A separate generated database exercises numeric collision compatibility and capacity refusal.
        string database = "SIAMIS_F51A_Test_" + Guid.NewGuid().ToString("N");
        var connection = new SqlConnectionStringBuilder { DataSource = "localhost", InitialCatalog = database, IntegratedSecurity = true, TrustServerCertificate = true };
        var options = new DbContextOptionsBuilder<SIAMISDbContext>().UseSqlServer(connection.ConnectionString).Options;
        try
        {
            await using var db = new SIAMISDbContext(options);
            await db.GetService<IMigrator>().MigrateAsync("20261008061002_AddEmployeeClockingFoundation");
            var legacy = new Employee { EmployeeNumber = "00100050", FirstName = "Numeric", LastName = "Legacy", IsActive = false };
            db.Employees.Add(legacy); await db.SaveChangesAsync();
            var script = db.GetService<IMigrator>().GenerateScript("20261008061002_AddEmployeeClockingFoundation", "20261009064219_AddPermanentEmployeeNumbering", MigrationsSqlGenerationOptions.Idempotent);
            await db.Database.OpenConnectionAsync();
            try
            {
                for (int pass = 0; pass < 2; pass++)
                    foreach (string batch in System.Text.RegularExpressions.Regex.Split(script, @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        if (!string.IsNullOrWhiteSpace(batch)) await db.Database.ExecuteSqlRawAsync(batch);
            }
            finally { await db.Database.CloseConnectionAsync(); }
            check(await db.EmployeeNumberReservations.CountAsync() == 1, "idempotent migration SQL executes and safely repeats");
            var assigned = await EmployeeNumberAllocator.ReserveAsync(connection.ConnectionString, Guid.NewGuid(), default);
            check(assigned == "100051", "numeric legacy collision preflight advances sequence safely");
            check(await db.Employees.AnyAsync(x => x.EmployeeId == legacy.EmployeeId && x.EmployeeNumber == "00100050" && !x.IsActive), "numeric legacy formatting and GUID remain unchanged");
            await db.Database.EnsureDeletedAsync();
            db.ChangeTracker.Clear();
            await db.GetService<IMigrator>().MigrateAsync("20261008061002_AddEmployeeClockingFoundation");
            db.Employees.Add(new Employee { EmployeeNumber = "9223372036854775806", FirstName = "Capacity", LastName = "Fixture", IsActive = false });
            await db.SaveChangesAsync();
            bool refused = false;
            try { await db.Database.MigrateAsync(); } catch (SqlException e) when (e.Number == 51000) { refused = true; }
            check(refused && !(await db.Database.GetAppliedMigrationsAsync()).Contains("20261009064219_AddPermanentEmployeeNumbering"), "capacity preflight aborts migration without recording success");
        }
        finally
        {
            await using var db = new SIAMISDbContext(options);
            await db.Database.EnsureDeletedAsync();
            Console.WriteLine("Disposable preflight database removed.");
        }
    }
}
