using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Services;

// Repository contract probes only. Synthetic values are not Thai legal parameters.
internal static class D6AContractRegressionTests
{
    public static void Run(Action<bool, string> check, IModel model)
    {
        var component = new PayrollComponent { Code = "SYNTHETIC", Name = "Synthetic", Category = "Earning", IsTaxable = true };
        var calculator = new PayrollCalculationService();
        var employee = new PayrollCalculationEmployee(Guid.NewGuid(), "SYNTHETIC", "Synthetic");
        var before = calculator.Calculate(employee, 100m, [], component, []);
        component.IsTaxable = false;
        check(before.TaxableEarnings == 100m && before.Lines.Single().IsTaxableSnapshot,
            "D6A generated tax snapshot survives live component reclassification");
        var after = calculator.Calculate(employee, 100m, [], component, []);
        check(after.TaxableEarnings == 0m && before.GrossPay == after.GrossPay,
            "D6A deliberate recalculation reads current tax flag without changing gross earnings");
        check(model.FindEntityType(typeof(PayrollComponent))!.FindProperty("IsTaxable")!.ClrType == typeof(bool),
            "D6A records existing boolean tax-classification limitation");

        var apply = typeof(EmployeePayrollService).GetMethod("ApplyLine", BindingFlags.Static | BindingFlags.NonPublic)!;
        component.IsTaxable = true;
        var line = new EmployeePayrollLine();
        var request = new EmployeePayrollLineRequest { PayrollComponentId = component.Id, Amount = 10m, Remarks = "Synthetic probe" };
        apply.Invoke(null, [line, request, component]);
        check(line.SourceType == "Manual" && line.SourceId is null && line.IsTaxableSnapshot,
            "D6A manual creation captures component tax flag and truthful provenance");
        component.IsTaxable = false;
        request.Amount = 20m;
        apply.Invoke(null, [line, request, component]);
        check(line.IsTaxableSnapshot, "D6A manual amount update preserves tax snapshot");
        var other = new PayrollComponent { Code = "OTHER", Name = "Other", Category = "Earning", IsTaxable = false };
        request.PayrollComponentId = other.Id;
        apply.Invoke(null, [line, request, other]);
        check(!line.IsTaxableSnapshot && line.SourceType == "Manual", "D6A manual component change captures new tax flag");

        var brackets = typeof(StatutoryPolicyService).GetMethod("ValidateBrackets", BindingFlags.Static | BindingFlags.NonPublic)!;
        PitTaxBracketRequest B(int order, decimal lower, decimal? upper, decimal rate = 0m)
            => new() { SortOrder = order, LowerBoundInclusive = lower, UpperBoundExclusive = upper, Rate = rate };
        bool Valid(bool complete, params PitTaxBracketRequest[] values)
            => brackets.Invoke(null, [values, complete]) is null;
        check(Valid(true, B(1, 0, 10), B(2, 10, null, 1)), "D6A contiguous zero-to-infinity brackets accepted");
        check(!Valid(true, B(1, 0, 10), B(2, 11, null)), "D6A publication rejects bracket gap");
        check(Valid(false, B(1, 0, 10), B(2, 11, null)), "D6A Draft may retain unfinished bracket gaps");
        check(!Valid(true, B(1, 0, 10), B(2, 9, null)), "D6A bracket overlap rejected");
        check(!Valid(true, B(1, 1, null)), "D6A publication rejects nonzero first boundary");
        check(!Valid(true, B(1, 0, 10)), "D6A publication requires final unbounded bracket");
        check(!Valid(true, B(1, 0, null), B(2, 10, null)), "D6A unbounded bracket cannot precede another");
        check(!Valid(true, B(2, 0, null)), "D6A deterministic consecutive order required");
        check(!Valid(true, B(1, 0, 0), B(2, 0, null)), "D6A bounded bracket requires positive width");
        check(!Valid(true, B(1, 0, null, -1)), "D6A negative bracket rate rejected");
        check(!Valid(true, B(1, -1, null)), "D6A negative boundary rejected");

        var opening = typeof(EmployeeStatutoryService).GetMethod("OpeningError", BindingFlags.Static | BindingFlags.NonPublic)!;
        bool OpeningValid(string state, decimal? income, decimal? tax, decimal? sso, string? remarks)
            => opening.Invoke(null, [state, income, tax, sso, remarks]) is null;
        check(OpeningValid("Unknown", null, null, null, null), "D6A explicit Unknown retains null monetary inputs");
        check(!OpeningValid("Unknown", 0m, null, null, null), "D6A Unknown cannot masquerade as confirmed zero");
        check(OpeningValid("ConfirmedZero", 0m, 0m, 0m, "Synthetic confirmation"), "D6A confirmed zero accepted explicitly");
        check(!OpeningValid("ConfirmedZero", 1m, 0m, 0m, "Synthetic"), "D6A confirmed zero rejects nonzero amount");
        check(!OpeningValid("VerifiedAmount", 10m, null, 1m, "Synthetic"), "D6A known balance requires all three amounts");
        check(OpeningValid("VerifiedAmount", 10m, 2m, 1m, "Synthetic"), "D6A structurally complete known balance accepted");
        check(!OpeningValid("VerifiedAmount", 10m, 2m, 1m, ""), "D6A known balance requires explanation");
        var selection = model.FindEntityType(typeof(EmployeeTaxDeclarationSelection))!;
        check(selection.FindPrimaryKey()!.Properties.Select(p => p.Name).SequenceEqual(["EmployeeId", "TaxYear"]),
            "D6A at most one declaration selection per employee/year");
        check(selection.GetForeignKeys().Single().Properties.Select(p => p.Name)
            .SequenceEqual(["EmployeeId", "TaxYear", "CurrentDeclarationId"]), "D6A selection FK preserves employee/year ownership");
        var declaration = model.FindEntityType(typeof(EmployeeTaxDeclaration))!;
        check(declaration.GetIndexes().Any(i => i.IsUnique && i.GetFilter() == "[Status] = 'Draft'"),
            "D6A only one Draft per employee/year");
        var balance = model.FindEntityType(typeof(EmployeeTaxOpeningBalance))!;
        check(balance.FindPrimaryKey()!.Properties.Single().Name == "EmployeeTaxDeclarationId",
            "D6A opening balance identity is declaration shared key, not separate invented ID");
        check(model.FindEntityType(typeof(EmployeePayrollLine))!.GetForeignKeys().All(f => f.Properties.All(p => p.Name != "SourceId")),
            "D6A historical SourceId remains snapshot data without FK");
    }
}
