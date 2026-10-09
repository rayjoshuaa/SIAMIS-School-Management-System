using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SIAMIS.Api.Security;
using SIAMIS.Api.Controllers;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Security;
using SIAMIS.Infrastructure.Services;

// Runs only inside the existing guarded disposable SQL + real Identity/CSRF host.
internal static class F51E1IdempotencyTests
{
    public static async Task RunAsync(IServiceProvider services, HttpClient admin, HttpClient hr, HttpClient payroll, HttpClient anonymous, Dictionary<string, object?> originalInput, string temporaryFixturePassword)
    {
        int checks = 0;
        void Check(bool pass, string label) { if (!pass) throw new InvalidOperationException("FAIL: " + label); checks++; Console.WriteLine("PASS: " + label); }
        var input = new Dictionary<string, object?>(originalInput) { ["firstName"] = "Idempotency" };
        async Task<HttpResponseMessage> Send(HttpClient client, Guid key, object? payload = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/employees") { Content = JsonContent.Create(payload ?? input) };
            request.Headers.Add("Idempotency-Key", key.ToString("D"));
            return await client.SendAsync(request);
        }
        async Task<(int Employees, int Employment, int Reservations, int Receipts, int Users, int Audit, long Sequence)> Counts()
        {
            await using var scope = services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
            return (await db.Employees.CountAsync(), await db.EmploymentRecords.CountAsync(), await db.EmployeeNumberReservations.CountAsync(),
                await db.EmployeeRegistrationReceipts.CountAsync(), await db.Users.CountAsync(), await db.Set<SecurityAuditEvent>().CountAsync(),
                await db.Database.SqlQueryRaw<long>("SELECT CONVERT(bigint,current_value) AS [Value] FROM sys.sequences WHERE name='EmployeeNumberSequence'").SingleAsync());
        }
        var before = await Counts(); var key = Guid.NewGuid();
        using var first = await Send(admin, key); string firstBody = await first.Content.ReadAsStringAsync();
        Check(first.StatusCode == HttpStatusCode.Created, "first keyed registration returns 201");
        var afterFirst = await Counts();
        Check(afterFirst.Employees == before.Employees + 1 && afterFirst.Employment == before.Employment + 1 && afterFirst.Receipts == before.Receipts + 1,
            "employee, initial employment and completed receipt commit together");
        Check(afterFirst.Reservations == before.Reservations + 1 && afterFirst.Sequence == before.Sequence + 1 && afterFirst.Users == before.Users,
            "one number allocated; account provisioning remains separate");
        Guid id = JsonDocument.Parse(firstBody).RootElement.GetProperty("employeeId").GetGuid();
        for (int i = 0; i < 2; i++)
        {
            using var replay = await Send(admin, key);
            Check(replay.StatusCode == HttpStatusCode.Created && await replay.Content.ReadAsStringAsync() == firstBody && replay.Headers.Location == first.Headers.Location,
                "sequential/double-submit replay returns original body and Location");
        }
        Check(await Counts() == afterFirst, "successful replays change no records, audit, reservations or sequence");
        var equivalent = new Dictionary<string, object?>(input) { ["firstName"] = "  Idempotency  ", ["middleName"] = " ", ["contacts"] = Array.Empty<object>() };
        using var normalized = await Send(admin, key, equivalent);
        Check(normalized.StatusCode == HttpStatusCode.Created && await normalized.Content.ReadAsStringAsync() == firstBody, "normalized strings and absent/empty children are equivalent");
        using var mismatch = await Send(admin, key, new Dictionary<string, object?>(input) { ["lastName"] = "Different" });
        Check(mismatch.StatusCode == HttpStatusCode.Conflict && (await mismatch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString() == "idempotency_conflict",
            "same scoped key with different payload returns structured 409");
        Check(await Counts() == afterFirst, "conflicting request does not allocate a number");
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
            var settings = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
            if (settings.DataSource != "localhost" || !System.Text.RegularExpressions.Regex.IsMatch(settings.InitialCatalog, "^SIAMIS_F51A_Test_[a-f0-9]{32}$"))
                throw new InvalidOperationException("Second API host requires the guarded disposable database.");
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", Args = [] });
            builder.Configuration.Sources.Clear(); builder.Configuration.AddInMemoryCollection(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.AddSiamisSecurity();
            builder.Services.RemoveAll<IHrSecurityReadService>();
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddDbContext<SIAMISDbContext>(o => o.UseSqlServer(settings.ConnectionString));
            builder.Services.Configure<SIAMIS.Infrastructure.Documents.PrivateDocumentOptions>(_ => { });
            builder.Services.AddSingleton<IPrivateDocumentStorage, SIAMIS.Infrastructure.Documents.PrivateDocumentStorage>();
            builder.Services.AddScoped<IEmployeeService, EmployeeService>();
            builder.Services.AddControllers().AddApplicationPart(typeof(EmployeesController).Assembly);
            await using var secondHost = builder.Build();
            secondHost.UseAuthentication(); secondHost.UseAuthorization(); secondHost.UseRateLimiter(); secondHost.MapControllers();
            await secondHost.StartAsync();
            using var secondClient = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false })
            { BaseAddress = new Uri(secondHost.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            async Task Csrf() { secondClient.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); var csrf = await secondClient.GetFromJsonAsync<JsonElement>("/api/auth/csrf"); secondClient.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString()); }
            await Csrf();
            Check((await secondClient.PostAsJsonAsync("/api/auth/login", new { userName = "number-admin", password = temporaryFixturePassword })).IsSuccessStatusCode,
                "second independent API host authenticates through real Identity");
            await Csrf(); using var crossHostReplay = await Send(secondClient, key);
            Check(crossHostReplay.StatusCode == HttpStatusCode.Created && await crossHostReplay.Content.ReadAsStringAsync() == firstBody,
                "independent API instance retrieves original actor-scoped receipt");
            var crossBefore = await Counts(); Guid crossKey = Guid.NewGuid();
            var crossResults = await Task.WhenAll(Send(admin, crossKey), Send(secondClient, crossKey));
            Check(crossResults.All(x => x.StatusCode == HttpStatusCode.Created) && await crossResults[0].Content.ReadAsStringAsync() == await crossResults[1].Content.ReadAsStringAsync(),
                "concurrent requests across two API hosts return the same result");
            var crossAfter = await Counts();
            Check(crossAfter.Employees == crossBefore.Employees + 1 && crossAfter.Receipts == crossBefore.Receipts + 1 && crossAfter.Sequence == crossBefore.Sequence + 1,
                "cross-instance concurrency commits one employee and consumes one number");
            foreach (var response in crossResults) response.Dispose();
            await secondHost.StopAsync();
        }

        // Mutate only an isolated fixture to prove that replay is the original result, not a current-profile reread.
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
            var employee = await db.Employees.SingleAsync(x => x.EmployeeId == id); employee.FirstName = "Edited later"; await db.SaveChangesAsync();
        }
        using var historical = await Send(admin, key);
        Check(await historical.Content.ReadAsStringAsync() == firstBody, "replay preserves original result after later profile edit");

        Guid concurrentKey = Guid.NewGuid(); var concurrentBefore = await Counts();
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Send(admin, concurrentKey)));
        var bodies = new List<string>(); foreach (var response in concurrent) { Check(response.StatusCode == HttpStatusCode.Created, "concurrent same-key HTTP request succeeds"); bodies.Add(await response.Content.ReadAsStringAsync()); response.Dispose(); }
        var concurrentAfter = await Counts();
        Check(bodies.Distinct().Count() == 1 && concurrentAfter.Employees == concurrentBefore.Employees + 1 && concurrentAfter.Employment == concurrentBefore.Employment + 1,
            "eight independent request scopes commit exactly one employee/employment");
        Check(concurrentAfter.Receipts == concurrentBefore.Receipts + 1 && concurrentAfter.Sequence == concurrentBefore.Sequence + 1 && concurrentAfter.Reservations == concurrentBefore.Reservations + 1,
            "concurrent same-key requests use exactly one receipt and employee number");

        Guid lostKey = Guid.NewGuid();
        using (var ignoredSuccessfulResponse = await Send(admin, lostKey)) { Check(ignoredSuccessfulResponse.StatusCode == HttpStatusCode.Created, "lost-response simulation commits normally"); }
        var lostBeforeRetry = await Counts();
        using var recovered = await Send(admin, lostKey);
        Check(recovered.StatusCode == HttpStatusCode.Created && await Counts() == lostBeforeRetry, "discarded successful response is recovered without second creation");
        using var separate = await Send(admin, Guid.NewGuid());
        Check(separate.StatusCode == HttpStatusCode.Created && (await separate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("employeeId").GetGuid() != id,
            "different key permits equivalent names/details");
        using var otherActor = await Send(hr, key);
        Check(otherActor.StatusCode == HttpStatusCode.Created && (await otherActor.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("employeeId").GetGuid() != id,
            "actor scope never discloses another user's stored registration");
        Check((await Send(payroll, key)).StatusCode == HttpStatusCode.Forbidden && (await Send(anonymous, key)).StatusCode == HttpStatusCode.Unauthorized,
            "authorization/authentication precede receipt replay");
        var token = admin.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN").Single(); admin.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Check((await Send(admin, key)).StatusCode == HttpStatusCode.BadRequest, "missing CSRF rejects even committed replay"); admin.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        using (var malformed = new HttpRequestMessage(HttpMethod.Post, "/api/employees") { Content = JsonContent.Create(input) })
        { malformed.Headers.Add("Idempotency-Key", "not-a-uuid"); Check((await admin.SendAsync(malformed)).StatusCode == HttpStatusCode.BadRequest, "malformed registration key is rejected"); }
        Check((await Send(admin, Guid.Empty)).StatusCode == HttpStatusCode.BadRequest, "empty UUID registration key is rejected");
        using (var missingKey = new HttpRequestMessage(HttpMethod.Post, "/api/employees") { Content = JsonContent.Create(input) })
        { missingKey.Headers.Add("Idempotency-Key", ""); Check((await admin.SendAsync(missingKey)).StatusCode == HttpStatusCode.BadRequest, "authenticated request without a usable key is rejected"); }
        using (var missing = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = admin.BaseAddress })
            Check((await missing.PostAsJsonAsync("/api/employees", input)).StatusCode == HttpStatusCode.Unauthorized, "anonymous missing key cannot expose validation/result data");

        Guid rollbackKey = Guid.NewGuid(); var rollbackBefore = await Counts();
        F51ANumberingTests.FailRegistrationReceipt = true;
        try { Check((await Send(admin, rollbackKey)).StatusCode == HttpStatusCode.InternalServerError, "receipt save fault returns failure"); }
        finally { F51ANumberingTests.FailRegistrationReceipt = false; }
        var rolledBack = await Counts();
        Check(rolledBack.Employees == rollbackBefore.Employees && rolledBack.Employment == rollbackBefore.Employment && rolledBack.Receipts == rollbackBefore.Receipts && rolledBack.Audit == rollbackBefore.Audit,
            "receipt fault rolls back employee, employment and authenticated audit atomically");
        Check(rolledBack.Reservations == rollbackBefore.Reservations + 1, "rollback preserves approved independent reservation gap");
        Check((await Send(admin, rollbackKey)).StatusCode == HttpStatusCode.Created, "uncommitted same-key failure is safely retryable");
        var teacherCode = "IDEMP-" + Guid.NewGuid().ToString("N")[..10];
        var decimalInput = new Dictionary<string, object?>(input) { ["teacherProfile"] = new { teacherCode, teachingStatus = "Active", yearsOfExperience = 1.00m } };
        var decimalKey = Guid.NewGuid(); using var decimalFirst = await Send(admin, decimalKey, decimalInput);
        decimalInput["teacherProfile"] = new { teacherCode, teachingStatus = "Active", yearsOfExperience = 1m };
        using var decimalReplay = await Send(admin, decimalKey, decimalInput);
        Check(decimalFirst.StatusCode == HttpStatusCode.Created && decimalReplay.StatusCode == HttpStatusCode.Created && await decimalFirst.Content.ReadAsStringAsync() == await decimalReplay.Content.ReadAsStringAsync(),
            "equivalent decimal representations produce the same canonical payload hash");

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
            var receipt = await db.EmployeeRegistrationReceipts.AsNoTracking().SingleAsync(x => x.RequestKey == key && x.ResultEmployeeId == id);
            Check(receipt.ReplayUntilUtc - receipt.CompletedAtUtc == TimeSpan.FromDays(30), "successful receipt has exact 30-day replay window");
            Guid expiredKey = Guid.NewGuid(); var expiredBefore = await Counts();
            db.EmployeeRegistrationReceipts.Add(new() { ActorUserId = receipt.ActorUserId, Operation = receipt.Operation, RequestKey = expiredKey, PayloadHash = receipt.PayloadHash,
                ResultEmployeeId = Guid.NewGuid(), ResultEmployeeNumber = "EXPIRED-FIXTURE", CompletedAtUtc = DateTime.UtcNow.AddDays(-31), ReplayUntilUtc = DateTime.UtcNow.AddDays(-1), ResponseJson = firstBody });
            await db.SaveChangesAsync();
            Check((await Send(admin, expiredKey)).StatusCode == HttpStatusCode.Gone, "expired key returns 410 even before response purge");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE EmployeeRegistrationReceipts SET ResponseJson=NULL WHERE RequestKey={expiredKey}");
            Check((await Send(admin, expiredKey)).StatusCode == HttpStatusCode.Gone, "expired permanent tombstone cannot create a second employee");
            var expiredAfter = await Counts();
            Check(expiredAfter.Employees == expiredBefore.Employees && expiredAfter.Sequence == expiredBefore.Sequence && expiredAfter.Reservations == expiredBefore.Reservations,
                "expiry rejection never allocates a number");
            async Task Rejected(string sql, string label, int expected)
            { bool denied = false; try { await db.Database.ExecuteSqlRawAsync(sql); } catch (SqlException e) when (e.Number == expected) { denied = true; } Check(denied, label); }
            await Rejected($"DELETE EmployeeRegistrationReceipts WHERE RequestKey='{expiredKey}'", "database prohibits deleting expired key protection", 51030);
            await Rejected($"UPDATE EmployeeRegistrationReceipts SET PayloadHash=CONVERT(varbinary(32),REPLICATE('X',32)) WHERE RequestKey='{key}'", "database prohibits replacing completion/hash", 51030);
            await Rejected($"UPDATE EmployeeRegistrationReceipts SET ResponseJson=NULL WHERE RequestKey='{key}'", "database prohibits premature response purge", 51030);
            // A deleted mistaken employee must not be resurrected by its historical receipt.
            var deletionKey = Guid.NewGuid(); using var created = await Send(admin, deletionKey);
            var deletionId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("employeeId").GetGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE EmploymentRecords WHERE EmployeeId={deletionId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE Employees WHERE EmployeeId={deletionId}");
            var deletedBeforeReplay = await Counts(); using var deletedReplay = await Send(admin, deletionKey);
            Check(deletedReplay.StatusCode == HttpStatusCode.Created && await Counts() == deletedBeforeReplay && !await db.Employees.AnyAsync(x => x.EmployeeId == deletionId),
                "historical replay never resurrects deleted employee or retired number");

            // Hold the exact SQL resource through another connection to test bounded in-progress behavior.
            Guid pendingKey = Guid.NewGuid();
            await using var connection = new SqlConnection(db.Database.GetConnectionString()); await connection.OpenAsync();
            await using var tx = (SqlTransaction)await connection.BeginTransactionAsync();
            await using var command = new SqlCommand("EXEC sys.sp_getapplock @Resource=@r, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=0", connection, tx);
            command.Parameters.AddWithValue("@r", $"Employee.Register.v1:{receipt.ActorUserId:D}:{pendingKey:D}"); await command.ExecuteNonQueryAsync();
            var pendingBefore = await Counts(); using var pending = await Send(admin, pendingKey);
            Check(pending.StatusCode == HttpStatusCode.Conflict && (await pending.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString() == "registration_in_progress"
                && pending.Headers.RetryAfter?.Delta == TimeSpan.FromSeconds(5), "bounded lock wait returns structured retryable in-progress result");
            Check(await Counts() == pendingBefore, "in-progress rejection leaves no pending row or allocation");
            await tx.RollbackAsync();
            Check((await Send(admin, pendingKey)).StatusCode == HttpStatusCode.Created, "same key succeeds after abandoned transaction releases lock");
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var hrUser = (await users.FindByNameAsync("number-hr"))!;
            await users.RemoveFromRoleAsync(hrUser, "HRAdmin"); await users.UpdateSecurityStampAsync(hrUser);
            var revoked = await Send(hr, key);
            Check(revoked.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, "revoked authenticated capability cannot retrieve committed receipt");
        }
        Console.WriteLine($"PASS: {checks} F5.1E.1 real SQL/HTTP/idempotency assertions.");
    }
}
