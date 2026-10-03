using System.Text.Json;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Focused view of the shared read-only payroll PIT path.</summary>
public sealed class PitCalculationPreviewService(IPayrollPreviewService payrollPreview) : IPitCalculationPreviewService
{
    public async Task<ServiceResult<PitCalculationResult>> PreviewAsync(Guid employeeId, Guid payrollPeriodId, CancellationToken ct)
    {
        var preview = await payrollPreview.PreviewAsync(payrollPeriodId, new() { EmployeeIds = [employeeId] }, ct);
        if (!preview.IsSuccess) return ServiceResult<PitCalculationResult>.Fail(preview.Failure!.Code, preview.Failure.Message);
        var current = preview.Value!.Results.Single();
        var pit = current.Pit;
        if (pit is null) return ServiceResult<PitCalculationResult>.Success(new("RequiresReview", [current.Message]));
        return ServiceResult<PitCalculationResult>.Success(new(pit.Status, pit.Reasons, pit.Snapshot?.Calculation,
            pit.Snapshot is null ? null : JsonSerializer.Serialize(pit.Snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
    }
}
