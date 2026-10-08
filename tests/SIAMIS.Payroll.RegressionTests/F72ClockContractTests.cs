using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;

internal static class F72ClockContractTests
{
    public static void Run(Action<bool, string> check, IModel model)
    {
        var session = model.FindEntityType(typeof(EmployeeClockSession))!;
        check(session.GetTableName() == "EmployeeClockSessions", "F72 additive session table");
        check(session.GetIndexes().Any(x => x.IsUnique && x.GetFilter() == "[OutEventId] IS NULL" && x.Properties.Single().Name == "EmployeeId"), "F72 database one-open-session constraint");
        check(session.GetForeignKeys().Count() == 3 && session.GetForeignKeys().All(x => x.DeleteBehavior == DeleteBehavior.NoAction), "F72 no cascading evidence or employee deletes");
        check(session.GetForeignKeys().Count(x => x.PrincipalEntityType.ClrType == typeof(AttendanceEvent) && x.Properties.First().Name == "EmployeeId") == 2, "F72 composite FKs enforce event employee ownership");
        check(session.GetIndexes().Any(x => x.IsUnique && x.Properties.SingleOrDefault()?.Name == "InEventId"), "F72 IN evidence belongs to one session");
        check(session.GetIndexes().Any(x => x.IsUnique && x.Properties.SingleOrDefault()?.Name == "OutEventId"), "F72 OUT evidence belongs to one session");
        check(session.FindProperty("WorkArrangement")!.GetMaxLength() == 20, "F72 bounded arrangement contract");
        foreach (var type in new[] { typeof(EmployeeClockInRequest), typeof(EmployeeClockOutRequest) })
            foreach (var name in new[] { "EmployeeId", "UserId", "OccurredAt", "Source", "ActorId", "ClockedInAtUtc", "ClockedOutAtUtc" })
                check(type.GetProperty(name) is null, "F72 caller cannot supply " + type.Name + "." + name);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var value in new[] { "OnCampus", "OnlineClass", "RemoteWork" })
            check(JsonSerializer.Deserialize<EmployeeClockInRequest>($"{{\"workArrangement\":\"{value}\"}}", json)!.WorkArrangement.ToString() == value, "F72 supported work arrangement " + value);
        foreach (var value in new[] { "0", "\"Unknown\"" })
        {
            try { JsonSerializer.Deserialize<EmployeeClockInRequest>($"{{\"workArrangement\":{value}}}", json); check(false, "F72 invalid arrangement rejected"); }
            catch (JsonException) { check(true, "F72 invalid arrangement rejected"); }
        }
        foreach (var extra in new[] { "employeeId", "occurredAt", "source", "actorId" })
        {
            try { JsonSerializer.Deserialize<EmployeeClockInRequest>($"{{\"{extra}\":\"forged\"}}", json); check(false, "F72 unknown fields rejected"); }
            catch (JsonException) { check(true, "F72 unknown fields rejected"); }
        }
        using var db = new SIAMISDbContext(new DbContextOptionsBuilder<SIAMISDbContext>().UseSqlServer("Server=localhost;Database=Unused;Integrated Security=True;TrustServerCertificate=True").Options);
        void Guard(EmployeeClockSession s, Action<EmployeeClockSession> mutation, bool allowed)
        {
            db.ChangeTracker.Clear(); db.Attach(s); mutation(s); db.ChangeTracker.DetectChanges();
            try { typeof(SIAMISDbContext).GetMethod("UpdateTimestamps", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(db, null); check(allowed, "F72 session mutation guard"); }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { check(!allowed, "F72 session mutation guard"); }
        }
        Guard(new() { WorkArrangement = "OnCampus" }, x => x.OutEventId = Guid.NewGuid(), true);
        Guard(new() { OutEventId = Guid.NewGuid() }, x => x.OutEventId = Guid.NewGuid(), false);
        Guard(new() { OutEventId = Guid.NewGuid() }, x => x.OutEventId = null, false);
        Guard(new(), x => x.InEventId = Guid.NewGuid(), false);
        Guard(new(), x => x.EmployeeId = Guid.NewGuid(), false);
        Guard(new() { WorkArrangement = "OnCampus" }, x => x.WorkArrangement = "RemoteWork", false);
        Guard(new(), x => db.Remove(x), false);
    }
}
