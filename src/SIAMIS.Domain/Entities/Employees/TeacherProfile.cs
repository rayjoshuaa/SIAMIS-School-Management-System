namespace SIAMIS.Domain.Entities.Employees;

public sealed class TeacherProfile
{
    public Guid TeacherProfileId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public string TeacherCode { get; set; } = string.Empty;
    public string? TeachingLevel { get; set; }
    public string? Specialization { get; set; }
    public decimal? YearsOfExperience { get; set; }
    public string TeachingStatus { get; set; } = string.Empty;
    public Employee Employee { get; set; } = null!;
}
