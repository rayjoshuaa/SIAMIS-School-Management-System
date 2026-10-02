using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Services;

internal static class D6BFoundationRegressionTests
{
    public static void Run(Action<bool, string> check, IModel model)
    {
        var resolver = new PitIncomeResolver();
        PitIncomeLine L(string treatment, decimal amount = 100, string type = "Earning")
            => new(Guid.NewGuid(), "SYNTHETIC", "Synthetic", type, amount, treatment, "Manual", null);
        check(resolver.Resolve([L("Included")]).IncludedIncomeCandidate == 100, "D6B Included earning contributes");
        check(resolver.Resolve([L("Excluded")]).IncludedIncomeCandidate == 0, "D6B Excluded earning excluded");
        check(resolver.Resolve([L("Unknown")]).Status == "Unresolved", "D6B Unknown earning unresolved");
        var mixed = resolver.Resolve([L("Included"), L("Excluded"), L("Unknown")]);
        check(mixed.Status == "Unresolved" && mixed.IncludedIncomeCandidate is null && mixed.Issues.Single().LineIndex == 2, "D6B mixed classification truthfully incomplete");
        check(resolver.Resolve([L("Included"), L("Unknown", type: "Deduction")]).IncludedIncomeCandidate == 100, "D6B deduction Unknown ignored");
        check(resolver.Resolve([L("Invalid")]).Status == "InvalidInput", "D6B invalid classification rejected");
        check(resolver.Resolve([L("Included", -1)]).Status == "InvalidInput", "D6B negative input rejected");
        check(resolver.Resolve([L("Excluded", -1)]).Status == "InvalidInput", "D6B excluded invalid amount not silently accepted");
        check(resolver.Resolve([L("Included", decimal.MaxValue), L("Included")]).Status == "InvalidInput", "D6B overflow is invalid");
        check(resolver.Resolve([]).Status == "Resolved" && resolver.Resolve([]).IncludedIncomeCandidate == 0, "D6B explicit empty candidate zero");
        check(resolver.Resolve([L("Included"), L("Included")]).IncludedIncomeCandidate == 200, "D6B distinct lines counted once each");
        foreach (var residence in new[] { "Unknown", "Resident", "NonResident" })
        foreach (var treatment in new[] { "Unknown", "StandardSection40_1", "RequiresReview" })
        {
            var input = new EmployeeTaxTreatmentDto(Guid.NewGuid(), Guid.NewGuid(), 2026, 1, residence, treatment, "Draft", null, "Synthetic");
            check(EmployeeTaxTreatmentResolver.Resolve(input).Status == "Unresolved", "D6B Draft cannot authorize " + residence + treatment);
            var verified = input with { Status = "Verified", VerifiedAt = DateTime.UtcNow };
            var expected = treatment == "RequiresReview" ? "Blocked"
                : residence == "Unknown" || treatment == "Unknown" ? "Unresolved" : "Approved";
            check(EmployeeTaxTreatmentResolver.Resolve(verified).Status == expected, "D6B verified gate " + residence + treatment);
        }
        check(EmployeeTaxTreatmentResolver.Resolve(null).Status == "Unresolved", "D6B no selected revision unresolved");
        check(typeof(EmployeeTaxTreatmentDto).GetProperties().All(p => !p.Name.Contains("Nationality") && !p.Name.Contains("Taxpayer")), "D6B gate has no nationality/taxpayer input");
        var component = new PayrollComponent { PitIncomeTreatment = "Included" };
        var line = new EmployeePayrollLine();
        var request = new EmployeePayrollLineRequest { PayrollComponentId = component.Id, Amount = 10, Remarks = "Synthetic" };
        var apply = typeof(EmployeePayrollService).GetMethod("ApplyLine", BindingFlags.Static | BindingFlags.NonPublic)!;
        apply.Invoke(null, [line, request, component]);
        check(line.PitIncomeTreatmentSnapshot == "Included" && line.SourceType == "Manual", "D6B manual creation snapshot");
        component.PitIncomeTreatment = "Excluded";
        apply.Invoke(null, [line, request, component]);
        check(line.PitIncomeTreatmentSnapshot == "Included", "D6B same component retains historical PIT snapshot");
        var changed = new PayrollComponent { PitIncomeTreatment = "Excluded" };
        request.PayrollComponentId = changed.Id;
        apply.Invoke(null, [line, request, changed]);
        check(line.PitIncomeTreatmentSnapshot == "Excluded", "D6B manual component change refreshes PIT snapshot");
        try { JsonSerializer.Deserialize<EmployeePayrollLineRequest>("{\"pitIncomeTreatmentSnapshot\":\"Included\"}"); check(false, "D6B forgery should fail"); }
        catch (JsonException) { check(true, "D6B client cannot forge PIT snapshot"); }
        foreach (var (type, field) in new[] { (typeof(PayrollComponent), "PitIncomeTreatment"), (typeof(EmployeePayrollLine), "PitIncomeTreatmentSnapshot") })
        {
            var metadata = model.FindEntityType(type)!;
            var property = metadata.FindProperty(field)!;
            check(!property.IsNullable && (string?)property.GetDefaultValue() == "Unknown", "D6B conservative SQL default " + field);
            check(metadata.GetCheckConstraints().Any(x => x.Sql.Contains("[" + field + "]") && x.Sql.Contains("'Excluded'")), "D6B SQL vocabulary " + field);
        }
        check(model.FindEntityType(typeof(PayrollComponent))!.GetSeedData().All(x => (string?)x["PitIncomeTreatment"] == "Unknown"), "D6B seeds never legally classified");
    }
}
