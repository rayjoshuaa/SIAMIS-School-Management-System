using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities;
using SIAMIS.Domain.Entities.Payroll;

internal static class D7OperationsContractTests
{
    public static void Run(Action<bool, string> check, IModel model)
    {
        check(PayrollDisplayName.Format("  Preferred  Name ", "First", " Middle ", " Last ") == "Preferred Name Middle Last", "D7 canonical whitespace-normalized name");
        check(PayrollDisplayName.Format(" ", " First ", null, "Last") == "First Last", "D7 blank preferred name falls back");
        var entity = model.FindEntityType(typeof(EmployeePayslip))!;
        check(entity.GetIndexes().Any(x => x.IsUnique && x.Properties.Single().Name == "EmployeePayrollId"), "D7 one payslip per payroll");
        check(entity.GetForeignKeys().Single().DeleteBehavior == DeleteBehavior.NoAction, "D7 payslip owner NoAction");
        check(entity.GetCheckConstraints().Any(x => x.Sql.Contains("ISJSON")), "D7 JSON check");
        check(entity.GetCheckConstraints().Any(x => x.Sql.Contains("[SnapshotVersion] = 1")), "D7 format version check");
        check(entity.FindProperty("CreatedAt")!.GetColumnType() == "datetime2" && entity.FindProperty("UpdatedAt")!.GetColumnType() == "datetime2", "D7 UTC datetime2 timestamps");
        var organization = model.FindEntityType(typeof(OrganizationProfile))!;
        check(organization.GetCheckConstraints().Any(x => x.Sql.Contains(OrganizationProfile.SingletonId.ToString())), "D7 singleton identity constraint");
        check(organization.GetForeignKeys().Count() == 0, "D7 employer is not multi-tenancy");
        check(!entity.GetForeignKeys().Any(x => x.PrincipalEntityType.ClrType == typeof(OrganizationProfile)), "D7 frozen organization is not mutable FK presentation");
        check(typeof(OrganizationProfileRequest).GetProperty("OrganizationProfileId") is null && typeof(OrganizationProfileRequest).GetProperty("CreatedAt") is null, "D7 identity/audit server controlled");
        var invalid = new OrganizationProfileRequest { DisplayName = " ", Email = "invalid" };
        check(!Validator.TryValidateObject(invalid, new ValidationContext(invalid), [], true), "D7 profile rejects invalid input");
        check(typeof(PayslipSnapshot).GetProperties().All(x => !x.Name.Contains("Declaration") && !x.Name.Contains("Claim")), "D7 no sensitive declarations in payslip");
        check(new EmployeePayslip().SnapshotVersion == 1, "D7 snapshot default format");
    }
}
