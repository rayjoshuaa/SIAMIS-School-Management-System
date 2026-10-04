using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Security;
using SIAMIS.Infrastructure.Security;

internal static class D12OffboardingTests
{
    public static void Run(Action<bool, string> check)
    {
        foreach (var decision in new bool?[] { null, true, false })
            foreach (var capability in new[] { false, true })
                check(EmployeeAccountLifecycleService.ValidateDecision(false, decision, capability, null, "current") is null,
                    "D12 no active account requires no redundant decision " + decision + capability);
        check(EmployeeAccountLifecycleService.ValidateDecision(true, null, true, "v", "v")?.Code == "active_linked_account_requires_offboarding_decision", "D12 stable unresolved access conflict");
        foreach (var decision in new[] { false, true })
        {
            check(EmployeeAccountLifecycleService.ValidateDecision(true, decision, false, "v", "v")?.Code == "forbidden", "D12 explicit access decision requires security capability " + decision);
            check(EmployeeAccountLifecycleService.ValidateDecision(true, decision, true, null, "v")?.Code == "conflict", "D12 current account version required " + decision);
            check(EmployeeAccountLifecycleService.ValidateDecision(true, decision, true, "old", "v")?.Code == "conflict", "D12 stale account version rejected " + decision);
            check(EmployeeAccountLifecycleService.ValidateDecision(true, decision, true, "v", "v") is null, "D12 explicit authorized access choice accepted " + decision);
        }
        foreach (var type in new[] { typeof(EndEmploymentRequest), typeof(RehireRequest), typeof(EmploymentChangeRequest) })
        {
            try { JsonSerializer.Deserialize("{\"ActorUserId\":\"forged\"}", type); check(false, "D12 strict lifecycle actor contract"); }
            catch (JsonException) { check(true, "D12 actor forgery rejected " + type.Name); }
        }
        var request = new EndEmploymentRequest { EndDate = DateOnly.FromDateTime(DateTime.UtcNow), EmploymentStatusId = Guid.NewGuid() };
        check(!Validator.TryValidateObject(request, new(request), [], true), "D12 expected employment identity mandatory");
        request.ExpectedEmploymentRecordId = Guid.NewGuid();
        check(Validator.TryValidateObject(request, new(request), [], true), "D12 no account choice required in request shape");
        foreach (var field in new[] { "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "AuthenticationToken" })
            check(typeof(EmployeeAccountLifecycleDto).GetProperty(field) is null, "D12 HR readiness excludes " + field);
        check(!SecurityCapabilities.ForRoles(["HRAdmin"]).Contains("Security.Manage"), "D12 HR does not gain security administration");
        check(SecurityCapabilities.ForRoles(["SystemAdmin"]).Contains("Employee.Manage") && SecurityCapabilities.ForRoles(["SystemAdmin"]).Contains("Security.Manage"), "D12 existing combined capability is available");
    }
}
