using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Domain.Entities.Employees;

public sealed class EmployeePerformance
{
    public Guid PerformanceRecordId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public DateOnly ReviewDate { get; set; }
    public DateOnly? ReviewPeriodStart { get; set; }
    public DateOnly? ReviewPeriodEnd { get; set; }
    public Guid PerformanceRatingId { get; set; }
    public Guid? ReviewerEmployeeId { get; set; }
    public string? Strengths { get; set; }
    public string? AreasForImprovement { get; set; }
    public string? Goals { get; set; }
    public string? Remarks { get; set; }

    public Employee Employee { get; set; } = null!;
    public PerformanceRating PerformanceRating { get; set; } = null!;
    public Employee? ReviewerEmployee { get; set; }
}
