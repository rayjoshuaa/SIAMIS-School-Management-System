using System.Text.Json;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Payroll;

internal static class PitSsoRecognitionContractTests
{
    public static void Run(Action<bool, string> check)
    {
        var payroll = new EmployeePayroll { Status = "Paid" };
        var result = new EmployeePayrollStatutoryResult { EmployeePayrollId = payroll.EmployeePayrollId,
            CalculationMethodVersion = "SSO-TH-V1" };
        result.SocialSecurity = new() { EmployeePayrollStatutoryResultId = result.EmployeePayrollStatutoryResultId,
            EmployeeAmount = 17, EmployerAmount = 999 };
        var cutoff = new DateOnly(2026, 1, 31);
        var prior = new DateOnly(2026, 2, 28);
        var current = new DateOnly(2026, 3, 31);
        PitSsoSource? History(DateOnly? date = null) => PitSsoRecognitionContract.Historical(payroll, date ?? prior, result, cutoff, current);
        bool Throws(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
        var historical = History()!;
        check(historical.SourceKind == PitSsoSourceKind.HistoricalPaidPayroll && historical.EmployeeAmount == 17, "D6C Paid historical employee SSO eligible, employer ignored");
        check(historical.EmployeePayrollStatutoryResultId == result.EmployeePayrollStatutoryResultId && historical.EmployeePayrollId == payroll.EmployeePayrollId, "D6C preserves exact D5 result and payroll IDs");
        foreach (var status in new[] { "Draft", "Calculated", "Approved", "Cancelled" }) {
            payroll.Status = status;
            check(History() is null, "D6C historical " + status + " SSO ineligible");
        }
        payroll.Status = "Paid";
        check(History(cutoff) is null, "D6C inclusive opening owns cutoff-day history");
        check(History(cutoff.AddDays(-1)) is null, "D6C no opening/history overlap");
        check(History(current.AddDays(1)) is null, "D6C future contribution never projected");
        check(History(new(2025, 12, 31)) is null, "D6C prior tax year excluded");
        check(Throws(() => History(current)), "D6C same-day historical ownership needs review");
        payroll.Status = "Calculated";
        var now = PitSsoRecognitionContract.Current(result, current, cutoff);
        check(now.SourceKind == PitSsoSourceKind.CurrentPayroll && now.EmployeeAmount == 17, "D6C current transaction does not require Paid");
        check(now.EmployeePayrollStatutoryResultId == result.EmployeePayrollStatutoryResultId, "D6C current exact D5 source preserved");
        check(Throws(() => PitSsoRecognitionContract.Current(result, cutoff, cutoff)), "D6C current cutoff overlap fails");
        var declaration = new EmployeeTaxDeclaration { EmployeeId = Guid.NewGuid(), TaxYear = 2026, Status = "Verified" };
        var selection = new EmployeeTaxDeclarationSelection { EmployeeId = declaration.EmployeeId,
            TaxYear = 2026, CurrentDeclarationId = declaration.EmployeeTaxDeclarationId };
        declaration.OpeningBalance = new() { EmployeeTaxDeclarationId = declaration.EmployeeTaxDeclarationId,
            State = "VerifiedAmount", InputContractVersion = "PIT-TH-V1", OpeningBalanceScope = "CurrentEmployer",
            CompletenessAttested = true, VerifiedAt = DateTime.UtcNow, AsOfDate = cutoff, PriorSocialSecurityContribution = 11 };
        var opening = PitSsoRecognitionContract.Opening(declaration, selection);
        check(opening.SourceKind == PitSsoSourceKind.OpeningBalance && opening.EmployeeAmount == 11 && opening.EmployeeTaxDeclarationId == declaration.EmployeeTaxDeclarationId, "D6C opening amount and shared-key identity distinct from D5 inputs");
        declaration.Status = "Draft";
        check(Throws(() => PitSsoRecognitionContract.Opening(declaration, selection)), "D6C Draft opening cannot contribute");
        declaration.Status = "Verified"; declaration.OpeningBalance.State = "Unknown";
        check(Throws(() => PitSsoRecognitionContract.Opening(declaration, selection)), "D6C Unknown opening unresolved");
        declaration.OpeningBalance.State = "ConfirmedZero"; declaration.OpeningBalance.PriorSocialSecurityContribution = 0;
        check(PitSsoRecognitionContract.Opening(declaration, selection).EmployeeAmount == 0, "D6C explicit confirmed-zero opening");
        selection.CurrentDeclarationId = Guid.NewGuid();
        check(Throws(() => PitSsoRecognitionContract.Opening(declaration, selection)), "D6C stale declaration not selected");
        var metadata = PitSsoRecognitionContract.Metadata;
        check(metadata.Mode == "ActualCumulative" && !metadata.ProjectsFutureContributions && !metadata.HasIndependentPitCap, "D6C no projection or independent cap contract");
        var json = JsonSerializer.Serialize(new[] { opening, historical, now });
        check(json.Contains("OpeningBalance") && json.Contains("HistoricalPaidPayroll") && json.Contains("CurrentPayroll") && !json.Contains("EmployerAmount"), "D6C future snapshot source discrimination and employee-only shape");
        check(payroll.BasicSalary == 0 && payroll.GrossPay == 0 && payroll.TotalDeductions == 0 && payroll.NetPay == 0, "D6C recognition does not mutate payroll money");
    }
}
