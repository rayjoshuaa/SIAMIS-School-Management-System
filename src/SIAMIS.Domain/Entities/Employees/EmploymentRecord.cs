using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Domain.Entities.Employees;

public sealed class EmploymentRecord
{
    public Guid EmploymentRecordId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? DesignationId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? EmploymentTypeId { get; set; }
    public Guid? EmploymentStatusId { get; set; }
    public Guid? HiringSourceId { get; set; }
    public Guid? ReportingToEmployeeId { get; set; }
    public DateOnly HireDate { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }

    public Employee Employee { get; set; } = null!;
    public Employee? ReportingToEmployee { get; set; }
    public Department? Department { get; set; }
    public Designation? Designation { get; set; }
    public Location? Location { get; set; }
    public EmploymentType? EmploymentType { get; set; }
    public EmploymentStatus? EmploymentStatus { get; set; }
    public HiringSource? HiringSource { get; set; }
}
