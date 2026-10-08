using System.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SIAMIS.Api.Security;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Security;

// Explicit operator tool, never part of API startup or a publicly callable endpoint.
// Fixed account and fixed local database; no arbitrary target/user/password arguments.
static void Guard(string? environment, string connection)
{
    if (environment != "Development") throw new InvalidOperationException("Development environment is required.");
    var target = new SqlConnectionStringBuilder(connection);
    if (target.DataSource != "localhost" || target.InitialCatalog != "SIAMIS" || !target.IntegratedSecurity)
        throw new InvalidOperationException("Only Windows-authenticated localhost/SIAMIS is permitted.");
}

if (args.SequenceEqual(new[] { "--verify-guards" }))
{
    const string local = "Server=localhost;Database=SIAMIS;Integrated Security=True";
    Guard("Development", local);
    var rejected = 0;
    foreach (var sample in new[] {
        ("Production", local), ("Staging", local),
        ("Development", "Server=remote;Database=SIAMIS;Integrated Security=True"),
        ("Development", "Server=localhost;Database=Other;Integrated Security=True"),
        ("Development", "Server=localhost;Database=SIAMIS;User Id=test;Password=test") })
    {
        try { Guard(sample.Item1, sample.Item2); }
        catch (InvalidOperationException) { rejected++; }
    }
    if (rejected != 5) throw new InvalidOperationException("Isolation verification failed.");
    Console.WriteLine("6 isolation checks passed; no database access or password entry.");
    return;
}
if (args.Length != 0) throw new InvalidOperationException("No account, password or configuration arguments are accepted.");
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
if (environment != "Development" || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") is string webEnvironment && webEnvironment != "Development")
    throw new InvalidOperationException("Explicit Development environment is required before any database access.");
if (Console.IsInputRedirected) throw new InvalidOperationException("Run interactively in your own terminal; redirected password input is prohibited.");

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment, ContentRootPath = Path.Combine(root, "src/SIAMIS.Api"), Args = [] });
var connection = builder.Configuration.GetConnectionString("SIAMIS") ?? "";
Guard(builder.Environment.EnvironmentName, connection);
builder.Logging.ClearProviders(); // This tool never logs credentials/tokens/Identity object values.
builder.AddSiamisSecurity(); // Exact application Identity validators, hashing and token provider.
builder.Services.AddDbContext<SIAMISDbContext>(options => options.UseSqlServer(connection));
// Resolve only Identity services; do not host HTTP or instantiate unrelated business services.
await using var services = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
using var scope = services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

static string ReadPassword(string prompt)
{
    Console.Write(prompt);
    var characters = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Escape) throw new OperationCanceledException("Cancelled before reset.");
        if (key.Key == ConsoleKey.Backspace) { if (characters.Count > 0) characters.RemoveAt(characters.Count - 1); }
        else if (!char.IsControl(key.KeyChar) && characters.Count < 256) characters.Add(key.KeyChar);
    }
    Console.WriteLine();
    var value = new string(characters.ToArray());
    characters.Clear();
    return value;
}
Console.WriteLine("Approved Development-only reset: f4-bootstrap-helper on localhost/SIAMIS.");
var password = ReadPassword("New password (not displayed; Escape cancels): ");
if (password != ReadPassword("Confirm new password (not displayed): ")) throw new InvalidOperationException("Passwords differ; no reset occurred.");
await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
await db.Roles.FromSqlRaw("SELECT * FROM [Roles] WITH (UPDLOCK) WHERE [NormalizedName] = 'SYSTEMADMIN'").AsNoTracking().SingleAsync();
var user = await db.Users.FromSqlRaw("SELECT * FROM [Users] WITH (UPDLOCK) WHERE [NormalizedUserName] = 'F4-BOOTSTRAP-HELPER'").SingleAsync();
var roles = await users.GetRolesAsync(user);
if (!user.IsActive || user.EmployeeId != null || roles.Count != 1 || roles[0] != "SystemAdmin")
    throw new InvalidOperationException("Unexpected helper identity/role/linkage drift; no reset occurred.");
var token = await users.GeneratePasswordResetTokenAsync(user);
var result = await users.ResetPasswordAsync(user, token, password);
password = ""; token = "";
if (!result.Succeeded) throw new InvalidOperationException("Identity rejected the reset: " + string.Join(", ", result.Errors.Select(e => e.Code)));
user.AdministrationVersion = Guid.NewGuid().ToString();
if (!(await users.UpdateAsync(user)).Succeeded) throw new InvalidOperationException("Concurrent account change; reset rolled back.");
// ResetPasswordAsync rotates SecurityStamp; existing application cookie validation rejects old sessions.
// No role, email, employee link, active state or lockout policy changes.
db.Add(new SecurityAuditEvent { ActorUserId = null, Operation = "DevelopmentAdministratorCredentialReset", ResourceType = "User", ResourceId = user.Id.ToString() });
await db.SaveChangesAsync();
await transaction.CommitAsync();
Console.WriteLine("Identity reset completed and audited. Sign in normally using your privately chosen password.");
