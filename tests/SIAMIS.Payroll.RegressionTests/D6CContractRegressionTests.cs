using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Services;

internal static class D6CContractRegressionTests
{
    public static void Run(Action<bool, string> check, IModel model)
    {
        var claim = typeof(EmployeeStatutoryService).GetMethod("ClaimInputError", BindingFlags.NonPublic | BindingFlags.Static)!;
        bool Valid(string type, decimal? amount = null, int? qty = 1, string? relation = null, bool? additional = null, string? reference = "Synthetic evidence")
            => claim.Invoke(null, [type, amount, qty, relation, additional, reference]) is null;
        check(Valid("Spouse"), "D6C spouse reviewed presence");
        check(Valid("Spouse", qty: null), "D6C spouse omitted quantity normalized to one");
        check(!Valid("Spouse", 10), "D6C rejects client allowance amount");
        check(!Valid("Spouse", qty: 2), "D6C rejects spouse multiplication");
        check(!Valid("Spouse", reference: null), "D6C evidence required");
        check(!Valid("Spouse", relation: "Lawful"), "D6C child-only metadata rejected on spouse");
        check(Valid("Child", relation: "Lawful", additional: false), "D6C lawful ordinary category");
        check(Valid("Child", relation: "Lawful", additional: true), "D6C lawful additional attested category");
        check(Valid("Child", relation: "Adopted", additional: false), "D6C adopted category");
        check(!Valid("Child", relation: "Adopted", additional: true), "D6C adopted additional tier rejected");
        check(!Valid("Child"), "D6C missing child facts rejected");
        check(!Valid("Child", relation: "lawful", additional: false), "D6C controlled category vocabulary");
        check(!Valid("Child", qty: null, relation: "Lawful", additional: false), "D6C explicit eligible count required");
        check(!Valid("Child", qty: 0, relation: "Lawful", additional: false), "D6C zero count rejected");
        check(Valid("Parent", qty: 2), "D6C reviewed parent count");
        check(!Valid("Parent", amount: 1), "D6C parent monetary entitlement not client-owned");
        check(!Valid("Donation"), "D6C unsupported deduction not ignored");
        var scope = typeof(EmployeeStatutoryService).GetMethod("OpeningScopeError", BindingFlags.NonPublic | BindingFlags.Static)!;
        bool Scope(string state, string? value, bool complete) => scope.Invoke(null, [state, value, complete]) is null;
        check(Scope("Unknown", null, false), "D6C Unknown remains unresolved");
        check(!Scope("Unknown", null, true), "D6C Unknown cannot assert completeness");
        check(Scope("ConfirmedZero", "CurrentEmployer", true), "D6C explicit complete scoped zero");
        check(Scope("VerifiedAmount", "CurrentEmployer", true), "D6C explicit scoped amounts");
        check(!Scope("VerifiedAmount", "PreviousEmployer", true), "D6C previous employer requires review");
        check(!Scope("ConfirmedZero", "CurrentEmployer", false), "D6C completeness not inferred");
        var policy = new StatutoryPolicyVersion {
            SchemeType = "PersonalIncomeTax", CalculationMethodVersion = "PIT-TH-V1", OfficialReference = "Synthetic only",
            EffectiveFrom = new(2026, 1, 1), EffectiveTo = new(2026, 12, 31),
            PersonalIncomeTax = new() { TaxYear = 2026, EmploymentExpenseDeductionRate = 10, EmploymentExpenseDeductionCap = 100,
                PersonalAllowanceAmount = 1, SpouseAllowanceAmount = 1, ChildAllowanceAmount = 1, AdditionalChildAllowanceAmount = 1,
                ParentAllowanceAmount = 1, AdoptedChildCombinedCountLimit = 2, MaximumEligibleParentCount = 2,
                WithholdingMethodIdentifier = "Synthetic", Brackets = [new() { SortOrder = 1, LowerBoundInclusive = 0, Rate = 0 }] }
        };
        var publish = typeof(StatutoryPolicyService).GetMethod("PublicationError", BindingFlags.NonPublic | BindingFlags.Static)!;
        string? Error() => (string?)publish.Invoke(null, [policy]);
        check(Error() is null, "D6C approved SSO recognition permits structurally complete publication");
        policy.PersonalIncomeTax.SpouseAllowanceAmount = null;
        check(Error()?.Contains("policy-owned") == true, "D6C V1 allowance completeness");
        policy.PersonalIncomeTax.SpouseAllowanceAmount = 1;
        policy.EffectiveTo = null;
        check(Error()?.Contains("TaxYear") == true, "D6C annual interval must be bounded");
        check(model.FindEntityType(typeof(EmployeeTaxClaim))!.FindProperty("Amount") is not null, "D6C legacy amount retained");
        check(model.FindEntityType(typeof(EmployeeTaxOpeningBalance))!.FindProperty("InputContractVersion") is not null, "D6C opening meaning versioned");
    }
}
