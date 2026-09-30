using Microsoft.EntityFrameworkCore;
using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Infrastructure.Data;

internal static class MasterDataSeeds
{
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static void Configure(ModelBuilder modelBuilder)
    {
        Seed<Department>(modelBuilder, "10000000", "DEP", new[]
        {
            "Leadership", "Ground Staff", "Marketing", "Teaching", "Procurement", "Production",
            "Engineering", "Human Resources", "Sales", "Finance", "Operations", "General Management"
        });
        Seed<Designation>(modelBuilder, "20000000", "DES", new[]
        {
            "Administration", "Business Executive", "Business Manager", "CEO", "Chinese", "Computing",
            "Customer Support Executive", "Director", "Engineer", "Finance Manager", "Account Manager",
            "High School Teacher", "Hiring Manager", "Housekeeper", "Human Resource Manager", "Junior Engineer",
            "Managing Director", "Marketing Executive", "Marketing Manager", "Math & Science"
        });
        Seed<EmploymentType>(modelBuilder, "30000000", "ET", new[] { "Full Time", "Part Time", "Contract", "Temporary", "Intern", "Probationary" });
        Seed<EmploymentStatus>(modelBuilder, "40000000", "ES", new[] { "Active", "Probation", "On Leave", "Suspended", "Resigned", "Terminated", "Retired", "End of Contract", "Inactive" });
        Seed<ContractType>(modelBuilder, "50000000", "CT", new[] { "Permanent", "Fixed Term", "Probationary", "Temporary", "Part Time", "Consultancy", "Internship" });
        Seed<Location>(modelBuilder, "60000000", "LOC", new[] { "Main Campus", "Secondary Campus", "Primary Campus", "Administration Office", "Other" });
        Seed<AddressType>(modelBuilder, "11000000", "ADDR", new[] { "Current", "Permanent", "Other" });
        Seed<Country>(modelBuilder, "12000000", "COUNTRY", new[] { "Thailand", "Philippines", "United Kingdom", "United States", "Australia", "Canada", "China", "Japan", "South Korea", "India", "France", "Germany", "Other" });
        Seed<HiringSource>(modelBuilder, "70000000", "HS", new[] { "Direct Application", "Employee Referral", "Recruitment Agency", "Job Website", "Social Media", "School Website", "Walk-in", "Internal Transfer", "Professional Network", "Other" });
        Seed<Nationality>(modelBuilder, "80000000", "NAT", new[] { "Thai", "Filipino", "British", "American", "Australian", "Canadian", "Chinese", "Japanese", "Korean", "Indian", "French", "German", "Other" });
        Seed<Gender>(modelBuilder, "90000000", "GEN", new[] { "Male", "Female", "Other", "Prefer Not to Say" });
        Seed<MaritalStatus>(modelBuilder, "A0000000", "MS", new[] { "Single", "Married", "Divorced", "Widowed", "Separated", "Prefer Not to Say" });
        Seed<DocumentType>(modelBuilder, "B0000000", "DOC", new[]
        {
            "National ID", "Passport", "Visa", "Work Permit", "Employment Contract", "Education Certificate",
            "Degree Certificate", "Teaching Certificate", "Professional License", "Training Certificate",
            "Medical Certificate", "Bank Account Document", "Photo", "Emergency Contact Document", "Other"
        });
        Seed<LeaveType>(modelBuilder, "C0000000", "LEV", new[] { "Annual", "Sick", "Personal", "Maternity", "Paternity", "Unpaid", "Compassionate", "Study", "Emergency", "Other" },
            (item, name) => item.IsPaid = name is "Annual" or "Sick" or "Maternity" or "Paternity");
        Seed<AttendanceStatus>(modelBuilder, "D0000000", "ATT", new[] { "Present", "Late", "Absent", "Half Day", "Early Leave", "On Leave", "Holiday", "Rest Day", "Business Trip", "Work From Home", "Other" });
        Seed<PayType>(modelBuilder, "E0000000", "PAY", new[] { "Monthly", "Daily", "Hourly", "Per Day", "Per Hour", "Other" });
        Seed<PayrollComponent>(modelBuilder, "F0000000", "EARN", new[] { "Basic Salary", "Housing Allowance", "Transportation Allowance", "Position Allowance", "Teaching Allowance", "Responsibility Allowance", "Overtime", "Bonus", "Commission", "Other Allowance" },
            (item, _) => item.Category = "Earning");
        Seed<PayrollComponent>(modelBuilder, "F1000000", "DEDUCT", new[] { "Social Security", "Withholding Tax", "Late Deduction", "Absence Deduction", "Loan Repayment", "Advance Salary", "Other Deduction" },
            (item, _) => item.Category = "Deduction");
        Seed<PerformanceRating>(modelBuilder, "F2000000", "PR", new[] { "Outstanding", "Exceeds Expectations", "Meets Expectations", "Needs Improvement", "Unsatisfactory" });
    }

    private static void Seed<T>(ModelBuilder modelBuilder, string guidPrefix, string codePrefix, string[] names, Action<T, string>? configure = null)
        where T : MasterDataEntity, new()
    {
        var rows = names.Select((name, index) =>
        {
            var item = new T
            {
                Id = Guid.Parse($"{guidPrefix}-0000-0000-0000-{index + 1:000000000000}"),
                Code = $"{codePrefix}-{index + 1:000}",
                Name = name,
                IsActive = true,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp
            };
            configure?.Invoke(item, name);
            return item;
        }).ToArray();

        modelBuilder.Entity<T>().HasData(rows);
    }
}
