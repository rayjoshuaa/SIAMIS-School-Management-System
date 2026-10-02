using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.Text.Json;
using SIAMIS.Application.MasterData;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Services;

// Dependency-free focused regression runner. No database connections or writes.
var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException($"FAIL: {name}");
    checks++;
}
var calculator = new PayrollCalculationService();
var employee = new PayrollCalculationEmployee(Guid.NewGuid(), "SYNTHETIC", "Synthetic test");
PayrollComponent Component(string code, string category = "Earning", string treatment = "Unknown") => new()
{
    Code = code, Name = code, Category = category, CalculationMethod = "FixedAmount",
    SsoWageTreatment = treatment, IsTaxable = true
};
EmployeePayrollComponentAssignment Assignment(PayrollComponent component, decimal amount) => new()
{
    EmployeeId = employee.EmployeeId, PayrollComponentId = component.Id, PayrollComponent = component, Amount = amount
};
ApplicablePayrollRuleDto Rule(PayrollComponent component, string mode, decimal amount, string method = "FixedAmount",
    string? baseType = null, decimal? rate = null) => new(
    Guid.NewGuid(), "RULE", "Synthetic rule", component.Id, component.Code, component.Name, component.Category,
    mode, component.Category, component.Category, 1, method, baseType, rate,
    method == "FixedAmount" ? amount : null, null, null, "Employee", new(2026, 1, 1), null,
    "Synthetic", [], component.IsTaxable, component.IsStatutory, component.ContributionSide, component.SsoWageTreatment, component.PitIncomeTreatment);
var basic = Component("BASIC");
var earning = Component("EARNING");
var other = Component("OTHER");
var deduction = Component("DEDUCTION", "Deduction");
var assignments = new[] { Assignment(earning, 1000), Assignment(other, 2000), Assignment(deduction, 500) };
PayrollCalculationResult Calculate(params ApplicablePayrollRuleDto[] rules) => calculator.Calculate(employee, 30000, assignments, basic, rules);
var baseline = Calculate();
Check(baseline.Status == "Calculated" && baseline.GrossPay == 33000 && baseline.TotalDeductions == 500 && baseline.NetPay == 32500, "baseline totals");
Check(baseline.Lines.All(x => x.SsoWageTreatmentSnapshot == "Unknown"), "unclassified lines stay Unknown");
Check(new PayrollComponent().SsoWageTreatment == "Unknown" && new EmployeePayrollLine().SsoWageTreatmentSnapshot == "Unknown", "entity defaults");
foreach (var treatment in new[] { "Unknown", "Included", "Excluded" })
{
    basic.SsoWageTreatment = earning.SsoWageTreatment = treatment;
    var rule = Rule(earning, "Supplement", 300);
    var result = Calculate(rule);
    Check(result.Status == "Calculated" && result.GrossPay == 33300 && result.NetPay == 32800 && result.TaxableEarnings == 33300, $"{treatment} does not change amounts");
    var salaryLine = result.Lines.Single(x => x.SourceType == "BasicSalary");
    var assignmentLine = result.Lines.Single(x => x.SourceId == assignments[0].EmployeePayrollComponentAssignmentId);
    var ruleLine = result.Lines.Single(x => x.SourceType == "PayrollRule");
    Check(salaryLine.SsoWageTreatmentSnapshot == treatment && salaryLine.SourceId == null, $"{treatment} BasicSalary snapshot/provenance");
    Check(assignmentLine.SsoWageTreatmentSnapshot == treatment && assignmentLine.SourceType == "Assignment", $"{treatment} assignment snapshot/provenance");
    Check(ruleLine.SsoWageTreatmentSnapshot == treatment && ruleLine.SourceId == rule.PayrollRuleId, $"{treatment} rule snapshot/provenance");
    basic.SsoWageTreatment = earning.SsoWageTreatment = treatment == "Included" ? "Excluded" : "Included";
    Check(salaryLine.SsoWageTreatmentSnapshot == treatment && assignmentLine.SsoWageTreatmentSnapshot == treatment && ruleLine.SsoWageTreatmentSnapshot == treatment, $"{treatment} snapshots unaffected by live classification changes");
    Check(rule.PayrollComponentSsoWageTreatment == treatment, $"{treatment} evaluator metadata is a value snapshot");
}
basic.SsoWageTreatment = earning.SsoWageTreatment = "Unknown";
var supplement = Calculate(Rule(earning, "Supplement", 300));
Check(supplement.GrossPay == 33300 && supplement.Lines.Count == 5, "earning Supplement regression");
var replace = Calculate(Rule(earning, "ReplaceAssignment", 300));
Check(replace.GrossPay == 32300 && !replace.Lines.Any(x => x.SourceId == assignments[0].EmployeePayrollComponentAssignmentId)
    && replace.Lines.Any(x => x.SourceId == assignments[1].EmployeePayrollComponentAssignmentId), "earning replacement only matching assignment");
var deductionSupplement = Calculate(Rule(deduction, "Supplement", 300));
Check(deductionSupplement.TotalDeductions == 800 && deductionSupplement.NetPay == 32200, "deduction Supplement regression");
var deductionReplace = Calculate(Rule(deduction, "ReplaceAssignment", 300));
Check(deductionReplace.TotalDeductions == 300 && deductionReplace.NetPay == 32700, "deduction replacement regression");
Check(Calculate(Rule(earning, "ReplaceAssignment", 100), Rule(earning, "ReplaceAssignment", 200)).Status == "Failed", "earning replacement conflict");
Check(Calculate(Rule(deduction, "ReplaceAssignment", 100), Rule(deduction, "ReplaceAssignment", 200)).Status == "Failed", "deduction replacement conflict");
Check(Calculate(Rule(earning, "Supplement", 0, "Percentage", "BasicSalary", 10)).GrossPay == 36000, "Percentage unchanged");
Check(Calculate(Rule(deduction, "Supplement", 0, "Percentage", "GrossPay", 10)).TotalDeductions == 3800, "deduction Percentage unchanged");
basic.IsTaxable = false; basic.IsStatutory = true; basic.ContributionSide = "Both";
var independent = Calculate();
Check(independent.Lines[0].SsoWageTreatmentSnapshot == "Unknown" && independent.TaxableEarnings == 3000, "SSO classification independent of tax/statutory/contribution-side flags");
foreach (var value in new string?[] { null, "Unknown", "Included", "Excluded", "invalid", "", "included" })
{
    var request = new PayrollComponentRequest { Code = "TEST", Name = "Test", ComponentType = "Earning", CalculationMethod = "FixedAmount", SsoWageTreatment = value };
    // DataAnnotations treats empty optional strings as absent; service validation rejects an explicit empty value.
    var valid = value is null or "" or "Unknown" or "Included" or "Excluded";
    Check(Validator.TryValidateObject(request, new ValidationContext(request), [], true) == valid, $"API annotation classification validation: {value ?? "omitted"}");
}
using var db = new SIAMISDbContext(new DbContextOptionsBuilder<SIAMISDbContext>()
    .UseSqlServer("Server=localhost;Database=Unused;Integrated Security=True;TrustServerCertificate=True").Options);
foreach (var invalid in new[] { "invalid", "", "included" })
{
    var failure = await new PayrollComponentService(db).CreatePayrollComponentAsync(new()
    { Code = "TEST", Name = "Test", ComponentType = "Earning", CalculationMethod = "FixedAmount", SsoWageTreatment = invalid }, default);
    Check(!failure.IsSuccess && failure.Failure?.Code == "validation", $"service rejects invalid treatment before any SQL: {invalid}");
}
// Relational design model, not the optimized runtime model, exposes defaults, checks, and seeds.
var model = db.GetService<IDesignTimeModel>().Model;
foreach (var (type, propertyName, constraint) in new[]
{
    (typeof(PayrollComponent), "SsoWageTreatment", "CK_PayrollComponents_SsoWageTreatment"),
    (typeof(EmployeePayrollLine), "SsoWageTreatmentSnapshot", "CK_EmployeePayrollLines_SsoWageTreatmentSnapshot")
})
{
    var entity = model.FindEntityType(type)!;
    var property = entity.FindProperty(propertyName)!;
    Check(!property.IsNullable && property.GetMaxLength() == 20 && (string?)property.GetDefaultValue() == "Unknown", $"{propertyName} required/default/length");
    Check(entity.GetCheckConstraints().Any(x => x.Name == constraint && x.Sql.Contains("'Unknown', 'Included', 'Excluded'")), $"{propertyName} SQL allowed-value constraint");
}
var seeds = model.FindEntityType(typeof(PayrollComponent))!.GetSeedData().ToArray();
Check(seeds.Length == 17 && seeds.All(x => (string?)x["SsoWageTreatment"] == "Unknown"), "all 17 seeded components Unknown");
Check(typeof(EmployeePayrollLineRequest).GetProperty("SsoWageTreatmentSnapshot") == null, "manual callers cannot set snapshots");
// Exercise the existing manual snapshot boundary without DB writes.
var applyLine = typeof(EmployeePayrollService).GetMethod("ApplyLine", BindingFlags.Static | BindingFlags.NonPublic)!;
var manualComponent = Component("MANUAL", treatment: "Included");
var manualLine = new EmployeePayrollLine();
var manualRequest = new EmployeePayrollLineRequest { PayrollComponentId = manualComponent.Id, Amount = 100, Remarks = "Synthetic" };
applyLine.Invoke(null, [manualLine, manualRequest, manualComponent]);
Check(manualLine.SourceType == "Manual" && manualLine.SourceId == null && manualLine.SsoWageTreatmentSnapshot == "Included", "manual creation truthful source and component snapshot");
manualComponent.SsoWageTreatment = "Excluded";
applyLine.Invoke(null, [manualLine, manualRequest, manualComponent]);
Check(manualLine.SsoWageTreatmentSnapshot == "Included", "manual amount update preserves existing classification snapshot");
var changedComponent = Component("CHANGED", treatment: "Excluded");
manualRequest.PayrollComponentId = changedComponent.Id;
applyLine.Invoke(null, [manualLine, manualRequest, changedComponent]);
Check(manualLine.SsoWageTreatmentSnapshot == "Excluded" && manualLine.SourceType == "Manual", "explicit manual component change snapshots new classification");
var migrations = db.GetService<IMigrationsAssembly>();
var migrationEntry = migrations.Migrations.Single(x => x.Key.EndsWith("_AddSsoWageTreatmentClassification"));
var migration = migrations.CreateMigration(migrationEntry.Value, "Microsoft.EntityFrameworkCore.SqlServer");
var columns = migration.UpOperations.OfType<AddColumnOperation>().ToArray();
Check(columns.Length == 2 && columns.All(x => !x.IsNullable && (string?)x.DefaultValue == "Unknown" && x.MaxLength == 20), "migration backfills both fields Unknown");
Check(migration.UpOperations.OfType<AddCheckConstraintOperation>().Count() == 2, "migration adds two allowed-value checks");
Check(migration.UpOperations.OfType<UpdateDataOperation>().All(x => x.Table == "PayrollComponents" && x.Columns.SequenceEqual(["SsoWageTreatment"]) && x.Values.Cast<object>().All(v => (string)v == "Unknown")), "migration seed updates only Unknown classification");
Check(migration.UpOperations.All(x => x is AddColumnOperation or AddCheckConstraintOperation or UpdateDataOperation), "migration has no monetary/schema operations outside classification");
var roundTrip = JsonSerializer.Deserialize<PayrollCalculatedLine>(JsonSerializer.Serialize(supplement.Lines.Single(x => x.SourceType == "PayrollRule")))!;
Check(roundTrip.SsoWageTreatmentSnapshot == "Unknown" && roundTrip.SourceType == "PayrollRule" && roundTrip.SourceId.HasValue, "snapshot serialization retains classification and provenance");
Section33WageRegressionTests.Run(Check);
Section33CalculationRegressionTests.Run(Check);
D6AContractRegressionTests.Run(Check, model);
foreach (var treatment in new[] { "Unknown", "Included", "Excluded" })
{
    var financialBefore = Calculate();
    basic.PitIncomeTreatment = earning.PitIncomeTreatment = treatment;
    var generated = Calculate(Rule(earning, "Supplement", 300));
    Check(generated.Lines.Single(x => x.SourceType == "BasicSalary").PitIncomeTreatmentSnapshot == treatment, "D6B BasicSalary snapshot " + treatment);
    Check(generated.Lines.Single(x => x.SourceId == assignments[0].EmployeePayrollComponentAssignmentId).PitIncomeTreatmentSnapshot == treatment, "D6B Assignment snapshot " + treatment);
    Check(generated.Lines.Single(x => x.SourceType == "PayrollRule").PitIncomeTreatmentSnapshot == treatment, "D6B PayrollRule snapshot " + treatment);
    var financialAfter = Calculate();
    Check(financialBefore.GrossPay == financialAfter.GrossPay && financialBefore.TaxableEarnings == financialAfter.TaxableEarnings
        && financialBefore.TotalDeductions == financialAfter.TotalDeductions && financialBefore.NetPay == financialAfter.NetPay,
        "D6B classification changes no legacy money " + treatment);
    basic.PitIncomeTreatment = earning.PitIncomeTreatment = "Unknown";
    Check(generated.Lines.Where(x => x.SourceType == "BasicSalary" || x.SourceType == "PayrollRule" || x.SourceId == assignments[0].EmployeePayrollComponentAssignmentId)
        .All(x => x.PitIncomeTreatmentSnapshot == treatment), "D6B live edits preserve value snapshots " + treatment);
}
D6BFoundationRegressionTests.Run(Check, model);
Console.WriteLine($"PASS: {checks} focused D5A/D5B/D5C/D6A/D6B regression assertions. No database connections or writes.");
