using SIAMIS.Domain.Common;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Domain.Entities.Employees;

public sealed class Employee : IHasTimestamps
{
    public Guid EmployeeId { get; set; } = Guid.NewGuid();
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string? PreferredName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Guid? GenderId { get; set; }
    public Guid? MaritalStatusId { get; set; }
    public Guid? NationalityId { get; set; }
    public string? ProfilePhoto { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Gender? Gender { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }
    public Nationality? Nationality { get; set; }
    public ICollection<EmployeeContact> Contacts { get; set; } = new List<EmployeeContact>();
    public ICollection<EmployeeAddress> Addresses { get; set; } = new List<EmployeeAddress>();
    public ICollection<EmergencyContact> EmergencyContacts { get; set; } = new List<EmergencyContact>();
    public ICollection<EmploymentRecord> EmploymentRecords { get; set; } = new List<EmploymentRecord>();
    public ICollection<EmployeeContract> Contracts { get; set; } = new List<EmployeeContract>();
    public ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();
    public ICollection<EmployeeDocument> VerifiedDocuments { get; set; } = new List<EmployeeDocument>();
    public TeacherProfile? TeacherProfile { get; set; }
    public ICollection<EmployeeCompensation> Compensations { get; set; } = new List<EmployeeCompensation>();
    public ICollection<EmployeeHistory> History { get; set; } = new List<EmployeeHistory>();
    public ICollection<EmployeeAttendance> AttendanceRecords { get; set; } = new List<EmployeeAttendance>();
    public ICollection<EmployeeLeave> Leaves { get; set; } = new List<EmployeeLeave>();
    public ICollection<EmployeePerformance> PerformanceRecords { get; set; } = new List<EmployeePerformance>();
    public ICollection<EmployeePayroll> PayrollRecords { get; set; } = new List<EmployeePayroll>();
    public ICollection<EmploymentRecord> DirectReports { get; set; } = new List<EmploymentRecord>();
}
