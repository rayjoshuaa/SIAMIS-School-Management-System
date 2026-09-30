using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Application.Payroll;

public sealed record PayrollCalculatedLine(
    Guid PayrollComponentId,
    string ComponentCode,
    string ComponentName,
    string ComponentType,
    string CalculationMethod,
    string? PercentageBase,
    decimal? Quantity,
    decimal? Rate,
    decimal Amount,
    string? Remarks);

public sealed record PayrollCalculationEmployee(Guid EmployeeId, string EmployeeNumber, string EmployeeName);

public sealed record PayrollCalculationResult(
    PayrollCalculationEmployee Employee,
    string Status,
    string Message,
    decimal BasicSalary,
    decimal GrossPay,
    decimal TotalDeductions,
    decimal NetPay,
    IReadOnlyList<PayrollCalculatedLine> Lines,
    string? Failure);

public interface IPayrollCalculationService
{
    PayrollCalculationResult Calculate(PayrollCalculationEmployee employee, decimal basicSalary,
        IReadOnlyList<EmployeePayrollComponentAssignment> assignments, PayrollComponent basicSalaryComponent);
}
