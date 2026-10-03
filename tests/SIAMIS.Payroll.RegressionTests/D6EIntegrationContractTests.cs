using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Payroll;

internal static class D6EIntegrationContractTests
{
    public static void Run(Action<bool, string> check, IModel model)
    {
        PitPaymentScheduleEntryRequest[] Entries(int start, int count) => Enumerable.Range(0, count)
            .Select(i => new PitPaymentScheduleEntryRequest(i + 1, new DateOnly(2026, start + i, 28))).ToArray();
        bool Valid(PitPaymentScheduleEntryRequest[] entries) => PitPaymentScheduleValidation.Validate(2026, "Reviewed synthetic schedule", entries, true) is null;
        var full = Entries(1, 12); var mid = Entries(7, 6);
        check(Valid(full), "D6E twelve reviewed monthly entries");
        check(Valid(mid), "D6E six reviewed monthly entries");
        check(!Valid([]), "D6E empty verification rejected");
        check(!Valid([full[0], full[0]]), "D6E duplicate dates/ordinals rejected");
        check(!Valid([full[0], full[1] with { PaymentOrdinal = 3 }]), "D6E gaps in ordinals rejected");
        check(!Valid([full[1] with { PaymentOrdinal = 1 }, full[0] with { PaymentOrdinal = 2 }]), "D6E reversed dates rejected");
        check(!Valid([full[0] with { PlannedPayDate = new(2027, 1, 28) }]), "D6E wrong year rejected");
        check(!Valid([full[0], full[1] with { PlannedPayDate = new(2026, 1, 29) }]), "D6E multiple payments in month rejected");
        check(!Valid([full[0], full[2] with { PaymentOrdinal = 2 }]), "D6E irregular monthly gap rejected");
        check(PitPaymentScheduleValidation.Validate(2026, " ", full, true) is not null, "D6E evidence required");
        check(PitPaymentScheduleValidation.Validate(2026, new string('x', 2001), full, true) is not null, "D6E evidence length");
        var result = model.FindEntityType(typeof(EmployeePayrollPitResult))!;
        check(result.GetIndexes().Any(x => x.IsUnique && x.Properties.SingleOrDefault()?.Name == "EmployeePayrollId"), "D6E one authoritative PIT result per payroll");
        check(result.GetForeignKeys().All(x => x.DeleteBehavior == DeleteBehavior.NoAction), "D6E PIT FKs NoAction");
        check(result.FindProperty("CurrentWithholding")!.GetColumnType() == "decimal(38,18)", "D6E preserves raw precision and satang result");
        check(result.GetCheckConstraints().Any(x => x.Sql.Contains("ISJSON")), "D6E valid JSON snapshot check");
        check(result.FindProperty("CreatedAt")!.GetColumnType() == "datetime2", "D6E UTC convention datetime2");
        var selection = model.FindEntityType(typeof(EmployeePitPaymentScheduleSelection))!;
        check(selection.FindPrimaryKey()!.Properties.Count == 2, "D6E one selection per employee/year");
        check(selection.GetForeignKeys().Single().Properties.Count == 3, "D6E selection cannot cross employee/year");
        var entry = model.FindEntityType(typeof(EmployeePitPaymentScheduleEntry))!;
        check(entry.GetIndexes().Count(x => x.IsUnique) == 2, "D6E unique schedule dates and ordinals");
        var line = model.FindEntityType(typeof(EmployeePayrollLine))!;
        check(!line.GetForeignKeys().Any(x => x.Properties.Any(p => p.Name == "SourceId")), "D6E statutory line SourceId remains snapshot, not FK");
        check(typeof(EmployeePayrollLineRequest).GetProperty("SourceType") is null && typeof(EmployeePayrollLineRequest).GetProperty("SourceId") is null,
            "D6E manual callers cannot forge statutory provenance");
    }
}
