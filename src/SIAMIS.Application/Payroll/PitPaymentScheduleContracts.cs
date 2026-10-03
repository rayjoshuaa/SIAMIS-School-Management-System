using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Payroll;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PitPaymentScheduleRequest
{
    [Range(1, 9999)] public int TaxYear { get; set; }
    [Required, StringLength(2000)] public string Evidence { get; set; } = string.Empty;
    public IReadOnlyList<PitPaymentScheduleEntryRequest> Entries { get; set; } = [];
}
public sealed record PitPaymentScheduleEntryRequest(int PaymentOrdinal, DateOnly PlannedPayDate);
public sealed record PitPaymentScheduleDto(Guid EmployeePitPaymentScheduleId, Guid EmployeeId, int TaxYear,
    int RevisionNumber, Guid? ReplacesScheduleId, string Status, string Evidence, DateTime CreatedAt,
    DateTime UpdatedAt, DateTime? VerifiedAt, bool IsCurrentVerified, IReadOnlyList<PitPaymentScheduleEntryRequest> Entries);
public sealed record PitScheduleResolution(string Status, string Message, PitPaymentScheduleDto? Schedule, PitPaymentSchedule? Payment);
public interface IPitPaymentScheduleService
{
    Task<ServiceResult<PitPaymentScheduleDto>> CreateAsync(Guid employeeId, PitPaymentScheduleRequest request, CancellationToken ct);
    Task<ServiceResult<PitPaymentScheduleDto>> GetAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<ServiceResult<PitPaymentScheduleDto>> UpdateDraftAsync(Guid employeeId, Guid id, PitPaymentScheduleRequest request, CancellationToken ct);
    Task<ServiceResult<PitPaymentScheduleDto>> VerifyAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<ServiceResult<PitPaymentScheduleDto?>> CurrentAsync(Guid employeeId, int taxYear, CancellationToken ct);
    Task<PitScheduleResolution> ResolveAsync(Guid employeeId, DateOnly payDate, CancellationToken ct);
}
/// <summary>Shared structural monthly validation. Does not reconstruct N from employment or period dates.</summary>
public static class PitPaymentScheduleValidation
{
    public static string? Validate(int year, string? evidence, IReadOnlyList<PitPaymentScheduleEntryRequest>? entries, bool verification)
    {
        if (year is < 1 or > 9999 || string.IsNullOrWhiteSpace(evidence) || evidence.Length > 2000)
            return "A valid Gregorian TaxYear and reviewed Evidence of at most 2000 characters are required.";
        if (entries is null || entries.Count > 12 || (verification && entries.Count == 0))
            return "A Verified monthly schedule requires 1 to 12 explicit payment entries.";
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e is null || e.PaymentOrdinal != i + 1 || e.PlannedPayDate.Year != year)
                return "Ordinals must start at 1 and be contiguous in increasing order; every date must belong to TaxYear.";
            if (i > 0 && (e.PlannedPayDate <= entries[i - 1].PlannedPayDate
                || e.PlannedPayDate.Month != entries[i - 1].PlannedPayDate.Month + 1))
                return "V1 supports one strictly increasing payment per consecutive month; duplicates, gaps or irregular schedules require review.";
        }
        return null;
    }
}
