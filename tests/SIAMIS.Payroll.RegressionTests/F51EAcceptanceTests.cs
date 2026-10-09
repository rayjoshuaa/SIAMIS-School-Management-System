using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIAMIS.Application.Employees;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Security;

// Runs only inside the guarded disposable F5.1A SQL/real Identity host.
internal static class F51EAcceptanceTests
{
    public static async Task RunAsync(HttpClient admin, HttpClient hr, HttpClient payroll, Dictionary<string, object?> input, IServiceProvider services)
    {
        int count = 0;
        void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
        var registered = await hr.PostAsJsonAsync("/api/employees", input);
        Check(registered.StatusCode == HttpStatusCode.Created, "acceptance registration succeeds without initial status");
        var employee = await registered.Content.ReadFromJsonAsync<JsonElement>();
        var id = employee.GetProperty("employeeId").GetGuid();
        var number = employee.GetProperty("employeeNumber").GetString();
        async Task<HttpClient> Persona(string role, Guid? link = null)
        {
            string name = "acceptance-" + role.ToLowerInvariant();
            string secret = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            await using (var scope = services.CreateAsyncScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = new ApplicationUser { UserName = name, IsActive = true, RequiresPasswordChange = false, EmployeeId = link };
                Check((await users.CreateAsync(user, secret)).Succeeded && (await users.AddToRoleAsync(user, role)).Succeeded, "isolated real Identity persona " + role);
            }
            var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = admin.BaseAddress };
            var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
            Check((await client.PostAsJsonAsync("/api/auth/login", new { userName = name, password = secret })).IsSuccessStatusCode, "real login " + role);
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
            return client;
        }
        using var management = await Persona("Management");
        using var self = await Persona("Employee", id);
        foreach (var (client, role, allowed) in new[] { (admin, "SystemAdmin", true), (hr, "HRAdmin", true), (payroll, "PayrollAdmin", false), (management, "Management", false), (self, "Employee", false) })
        {
            Check((await client.GetAsync($"/api/employees/{id}")).StatusCode == (allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden), role + " direct employee read boundary");
            Check((await client.GetAsync($"/api/employees/{id}/photo")).StatusCode == (allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden), role + " direct photo read boundary");
            Check((await client.GetAsync("/api/admin/users?page=1&pageSize=10")).StatusCode == (role == "SystemAdmin" ? HttpStatusCode.OK : HttpStatusCode.Forbidden), role + " Security.Manage boundary");
            if (!allowed)
                Check((await client.PutAsJsonAsync($"/api/employees/{id}", new { })).StatusCode == HttpStatusCode.Forbidden, role + " direct employee write denied");
        }
        var history = await hr.GetFromJsonAsync<EmploymentRecordDto[]>($"/api/employees/{id}/employment-history");
        Guid terminal, active;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
            terminal = await db.EmploymentStatuses.Where(x => x.IsActive && x.IsTerminal).Select(x => x.Id).FirstAsync();
            active = await db.EmploymentStatuses.Where(x => x.Code == "ES-001").Select(x => x.Id).SingleAsync();
        }
        var linkage = await admin.GetFromJsonAsync<EmployeeAccountLifecycleDto>($"/api/employees/{id}/account-lifecycle");
        var end = new { expectedEmploymentRecordId = history!.Single().EmploymentRecordId, endDate = "2026-09-20", employmentStatusId = terminal, disableLinkedAccount = true, expectedLinkedAccountVersion = linkage!.LinkedAccountVersion };
        Check((await hr.PostAsJsonAsync($"/api/employees/{id}/end-employment", end)).StatusCode == HttpStatusCode.Forbidden, "HR cannot decide linked active account without Security.Manage");
        Check((await admin.PostAsJsonAsync($"/api/employees/{id}/end-employment", new { expectedEmploymentRecordId = Guid.NewGuid(), endDate = "2026-09-20", employmentStatusId = terminal })).StatusCode == HttpStatusCode.Conflict, "stale employment version rejected");
        Check((await admin.PostAsJsonAsync($"/api/employees/{id}/end-employment", end)).StatusCode == HttpStatusCode.OK, "authorized End Employment succeeds atomically");
        var ended = await hr.GetFromJsonAsync<JsonElement>($"/api/employees/{id}");
        Check(!ended.GetProperty("isActive").GetBoolean(), "End Employment deactivates employee record");
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
            Check(!await db.Users.Where(x => x.EmployeeId == id).Select(x => x.IsActive).SingleAsync(), "explicit account offboarding preserved");
        }
        var rehire = new { hireDate = "2026-09-21", departmentId = input["departmentId"], designationId = input["designationId"], employmentTypeId = input["employmentTypeId"], employmentStatusId = active };
        Check((await hr.PostAsJsonAsync($"/api/employees/{id}/rehire", rehire)).StatusCode == HttpStatusCode.Created, "authorized rehire preserves employee identity");
        var rehired = await hr.GetFromJsonAsync<JsonElement>($"/api/employees/{id}");
        Check(rehired.GetProperty("isActive").GetBoolean() && rehired.GetProperty("employeeNumber").GetString() == number && rehired.GetProperty("employeeId").GetGuid() == id, "rehire preserves permanent number and GUID");
        var finalHistory = await hr.GetFromJsonAsync<EmploymentRecordDto[]>($"/api/employees/{id}/employment-history");
        Check(finalHistory!.Length == 2 && finalHistory.Single(x => !x.IsCurrent).EndDate == new DateOnly(2026, 9, 20), "closed historical employment retained");
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
            Check(!await db.Users.Where(x => x.EmployeeId == id).Select(x => x.IsActive).SingleAsync(), "rehire does not automatically reactivate account");
        }
        Console.WriteLine($"PASS: {count} F5.1E acceptance HTTP/Identity/lifecycle assertions.");
    }
}
