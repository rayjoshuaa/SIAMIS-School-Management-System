using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Employees;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees", table =>
        {
            table.HasTrigger("TR_Employees_PermanentIdentity");
            table.UseSqlOutputClause(false);
        });
        builder.HasKey(employee => employee.EmployeeId);
        builder.Property(employee => employee.EmployeeNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(employee => employee.EmployeeNumber).IsUnique();
        builder.Property(employee => employee.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(employee => employee.MiddleName).HasMaxLength(100);
        builder.Property(employee => employee.LastName).HasMaxLength(100).IsRequired();
        builder.Property(employee => employee.PreferredName).HasMaxLength(100);
        builder.Property(employee => employee.DateOfBirth).HasColumnType("date");
        builder.Property(employee => employee.ProfilePhoto).HasMaxLength(500);
        builder.Property(employee => employee.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(employee => employee.UpdatedAt).HasColumnType("datetime2").IsRequired();
        builder.HasIndex(employee => new { employee.LastName, employee.FirstName });

        builder.HasOne(employee => employee.Gender).WithMany().HasForeignKey(employee => employee.GenderId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(employee => employee.MaritalStatus).WithMany().HasForeignKey(employee => employee.MaritalStatusId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(employee => employee.Nationality).WithMany().HasForeignKey(employee => employee.NationalityId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmployeeContactConfiguration : IEntityTypeConfiguration<EmployeeContact>
{
    public void Configure(EntityTypeBuilder<EmployeeContact> builder)
    {
        builder.ToTable("EmployeeContacts");
        builder.HasKey(item => item.EmployeeContactId);
        builder.Property(item => item.WorkEmail).HasMaxLength(254);
        builder.Property(item => item.PersonalEmail).HasMaxLength(254);
        builder.Property(item => item.Mobile).HasMaxLength(30);
        builder.Property(item => item.Phone).HasMaxLength(30);
        builder.Property(item => item.WorkPhone).HasMaxLength(30);
        builder.HasIndex(item => item.EmployeeId);
        builder.HasIndex(item => item.EmployeeId).IsUnique().HasFilter("[IsPrimary] = 1").HasDatabaseName("UX_EmployeeContacts_PrimaryPerEmployee");
        builder.HasOne(item => item.Employee).WithMany(employee => employee.Contacts).HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmployeeAddressConfiguration : IEntityTypeConfiguration<EmployeeAddress>
{
    public void Configure(EntityTypeBuilder<EmployeeAddress> builder)
    {
        builder.ToTable("EmployeeAddresses");
        builder.HasKey(item => item.EmployeeAddressId);
        builder.Property(item => item.AddressLine1).HasMaxLength(200).IsRequired();
        builder.Property(item => item.AddressLine2).HasMaxLength(200);
        builder.Property(item => item.City).HasMaxLength(100);
        builder.Property(item => item.StateProvince).HasMaxLength(100);
        builder.Property(item => item.PostalCode).HasMaxLength(20);
        builder.HasIndex(item => item.EmployeeId);
        builder.HasIndex(item => item.EmployeeId).IsUnique().HasFilter("[IsPrimary] = 1").HasDatabaseName("UX_EmployeeAddresses_PrimaryPerEmployee");
        builder.HasOne(item => item.Employee).WithMany(employee => employee.Addresses).HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.AddressType).WithMany().HasForeignKey(item => item.AddressTypeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.Country).WithMany().HasForeignKey(item => item.CountryId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmergencyContactConfiguration : IEntityTypeConfiguration<EmergencyContact>
{
    public void Configure(EntityTypeBuilder<EmergencyContact> builder)
    {
        builder.ToTable("EmergencyContacts");
        builder.HasKey(item => item.EmergencyContactId);
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Relationship).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Mobile).HasMaxLength(30);
        builder.Property(item => item.Phone).HasMaxLength(30);
        builder.Property(item => item.Email).HasMaxLength(254);
        builder.Property(item => item.AlternativePhone).HasMaxLength(30);
        builder.Property(item => item.Address).HasMaxLength(500);
        builder.HasIndex(item => item.EmployeeId).HasDatabaseName("IX_EmergencyContacts_EmployeeId");
        builder.HasIndex(item => item.EmployeeId, "UX_EmergencyContacts_PrimaryPerEmployee")
            .IsUnique().HasFilter("[IsPrimary] = 1");
        builder.HasOne(item => item.Employee).WithMany(employee => employee.EmergencyContacts).HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmploymentRecordConfiguration : IEntityTypeConfiguration<EmploymentRecord>
{
    public void Configure(EntityTypeBuilder<EmploymentRecord> builder)
    {
        builder.ToTable("EmploymentRecords", table =>
        {
            table.HasCheckConstraint("CK_EmploymentRecords_StartDate", "[StartDate] IS NULL OR [StartDate] >= [HireDate]");
            table.HasCheckConstraint("CK_EmploymentRecords_EndDate", "[EndDate] IS NULL OR [EndDate] >= COALESCE([StartDate], [HireDate])");
            table.HasCheckConstraint("CK_EmploymentRecords_CurrentOpen", "[IsCurrent] = 0 OR [EndDate] IS NULL");
        });
        builder.HasKey(item => item.EmploymentRecordId);
        builder.Property(item => item.HireDate).HasColumnType("date");
        builder.Property(item => item.StartDate).HasColumnType("date");
        builder.Property(item => item.EndDate).HasColumnType("date");
        builder.HasIndex(item => item.EmployeeId);
        builder.HasIndex(item => item.EmployeeId).IsUnique().HasFilter("[IsCurrent] = 1").HasDatabaseName("UX_EmploymentRecords_CurrentPerEmployee");
        builder.HasIndex(item => item.ReportingToEmployeeId);
        builder.HasOne(item => item.Employee).WithMany(employee => employee.EmploymentRecords).HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.ReportingToEmployee).WithMany(employee => employee.DirectReports).HasForeignKey(item => item.ReportingToEmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.Department).WithMany().HasForeignKey(item => item.DepartmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.Designation).WithMany().HasForeignKey(item => item.DesignationId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.Location).WithMany().HasForeignKey(item => item.LocationId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.EmploymentType).WithMany().HasForeignKey(item => item.EmploymentTypeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.EmploymentStatus).WithMany().HasForeignKey(item => item.EmploymentStatusId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.HiringSource).WithMany().HasForeignKey(item => item.HiringSourceId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmployeeContractConfiguration : IEntityTypeConfiguration<EmployeeContract>
{
    public void Configure(EntityTypeBuilder<EmployeeContract> builder)
    {
        builder.ToTable("EmployeeContracts");
        builder.HasKey(item => item.EmployeeContractId);
        builder.Property(item => item.ContractNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(item => item.ContractNumber).IsUnique();
        builder.Property(item => item.StartDate).HasColumnType("date");
        builder.Property(item => item.EndDate).HasColumnType("date");
        builder.Property(item => item.ProbationEndDate).HasColumnType("date");
        builder.Property(item => item.ContractStatus).HasMaxLength(40).IsRequired();
        builder.Property(item => item.Notes).HasMaxLength(2000);
        builder.Property(item => item.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnType("datetime2").IsRequired();
        builder.HasIndex(item => item.EmployeeId);
        builder.HasOne(item => item.Employee).WithMany(employee => employee.Contracts).HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.ContractType).WithMany().HasForeignKey(item => item.ContractTypeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.Document).WithOne()
            .HasForeignKey<EmployeeContract>(item => new { item.EmployeeId, item.DocumentId })
            .HasPrincipalKey<EmployeeDocument>(item => new { item.EmployeeId, item.EmployeeDocumentId })
            .OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> builder)
    {
        builder.ToTable("EmployeeDocuments");
        builder.HasKey(item => item.EmployeeDocumentId);
        builder.HasAlternateKey(item => new { item.EmployeeId, item.EmployeeDocumentId });
        builder.Property(item => item.DocumentNumber).HasMaxLength(100);
        builder.Property(item => item.IssueDate).HasColumnType("date");
        builder.Property(item => item.ExpiryDate).HasColumnType("date");
        builder.Property(item => item.FileName).HasMaxLength(260).IsRequired();
        builder.Property(item => item.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(item => item.VerificationStatus).HasMaxLength(40).IsRequired();
        builder.Property(item => item.VerifiedAt).HasColumnType("datetime2");
        builder.Property(item => item.Remarks).HasMaxLength(2000);
        builder.Property(item => item.UploadedAt).HasColumnType("datetime2").IsRequired();
        builder.HasIndex(item => item.EmployeeId);
        builder.HasIndex(item => new { item.DocumentTypeId, item.DocumentNumber });
        builder.HasOne(item => item.Employee).WithMany(employee => employee.Documents).HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.Verifier).WithMany(employee => employee.VerifiedDocuments).HasForeignKey(item => item.VerifiedBy).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.DocumentType).WithMany().HasForeignKey(item => item.DocumentTypeId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class TeacherProfileConfiguration : IEntityTypeConfiguration<TeacherProfile>
{
    public void Configure(EntityTypeBuilder<TeacherProfile> builder)
    {
        builder.ToTable("TeacherProfiles");
        builder.HasKey(item => item.TeacherProfileId);
        builder.HasIndex(item => item.EmployeeId).IsUnique();
        builder.Property(item => item.TeacherCode).HasMaxLength(30).IsRequired();
        builder.HasIndex(item => item.TeacherCode).IsUnique();
        builder.Property(item => item.TeachingLevel).HasMaxLength(100);
        builder.Property(item => item.Specialization).HasMaxLength(200);
        builder.Property(item => item.YearsOfExperience).HasColumnType("decimal(4,1)");
        builder.Property(item => item.TeachingStatus).HasMaxLength(40).IsRequired();
        builder.HasOne(item => item.Employee).WithOne(employee => employee.TeacherProfile).HasForeignKey<TeacherProfile>(item => item.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmployeeCompensationConfiguration : IEntityTypeConfiguration<EmployeeCompensation>
{
    public void Configure(EntityTypeBuilder<EmployeeCompensation> builder)
    {
        builder.ToTable("EmployeeCompensations");
        builder.HasKey(item => item.EmployeeCompensationId);
        builder.Property(item => item.BasicSalary).HasColumnType("decimal(19,4)").IsRequired();
        builder.Property(item => item.Currency).HasColumnType("char(3)").IsRequired();
        builder.Property(item => item.EffectiveFrom).HasColumnType("date");
        builder.Property(item => item.EffectiveTo).HasColumnType("date");
        builder.Property(item => item.Remarks).HasMaxLength(2000);
        builder.HasIndex(item => item.EmployeeId);
        builder.HasIndex(item => item.EmployeeId).IsUnique().HasFilter("[IsCurrent] = 1").HasDatabaseName("UX_EmployeeCompensations_CurrentPerEmployee");
        builder.HasOne(item => item.Employee).WithMany(employee => employee.Compensations).HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.PayType).WithMany().HasForeignKey(item => item.PayTypeId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmployeeHistoryConfiguration : IEntityTypeConfiguration<EmployeeHistory>
{
    public void Configure(EntityTypeBuilder<EmployeeHistory> builder)
    {
        builder.ToTable("EmployeeHistory");
        builder.HasKey(item => item.EmployeeHistoryId);
        builder.Property(item => item.EventType).HasMaxLength(80).IsRequired();
        builder.Property(item => item.EventDate).HasColumnType("datetime2").IsRequired();
        builder.Property(item => item.PreviousValue).HasMaxLength(4000);
        builder.Property(item => item.NewValue).HasMaxLength(4000);
        builder.Property(item => item.Description).HasMaxLength(2000);
        builder.Property(item => item.ChangedBy).HasMaxLength(100);
        builder.HasIndex(item => new { item.EmployeeId, item.EventDate });
        builder.HasOne(item => item.Employee).WithMany(employee => employee.History).HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmployeeAttendanceConfiguration : IEntityTypeConfiguration<EmployeeAttendance>
{
    public void Configure(EntityTypeBuilder<EmployeeAttendance> builder)
    {
        builder.ToTable("Attendance");
        builder.HasKey(item => item.AttendanceId);
        builder.Property(item => item.AttendanceDate).HasColumnType("date").IsRequired();
        builder.Property(item => item.CheckIn).HasColumnType("time(0)");
        builder.Property(item => item.CheckOut).HasColumnType("time(0)");
        builder.Property(item => item.Remarks).HasMaxLength(2000);
        builder.HasIndex(item => new { item.EmployeeId, item.AttendanceDate })
            .IsUnique().HasDatabaseName("UX_Attendance_EmployeeId_AttendanceDate");
        builder.HasIndex(item => item.AttendanceStatusId);
        builder.HasOne(item => item.Employee).WithMany(employee => employee.AttendanceRecords)
            .HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.AttendanceStatus).WithMany()
            .HasForeignKey(item => item.AttendanceStatusId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmployeeLeaveConfiguration : IEntityTypeConfiguration<EmployeeLeave>
{
    public void Configure(EntityTypeBuilder<EmployeeLeave> builder)
    {
        builder.ToTable("EmployeeLeave");
        builder.ToTable("EmployeeLeave", table =>
        {
            table.HasCheckConstraint("CK_EmployeeLeave_RequestTimes", "([RequestedStartTime] IS NULL AND [RequestedEndTime] IS NULL) OR ([RequestedStartTime] IS NOT NULL AND [RequestedEndTime] IS NOT NULL AND DATEPART(SECOND,[RequestedStartTime]) = 0 AND DATEPART(SECOND,[RequestedEndTime]) = 0 AND ([EndDate] > [StartDate] OR ([EndDate] = [StartDate] AND [RequestedEndTime] > [RequestedStartTime])))");
            table.HasCheckConstraint("CK_EmployeeLeave_ChargeMinutes", "[ChargeableMinutes] IS NULL OR [ChargeableMinutes] >= 0");
            table.HasCheckConstraint("CK_EmployeeLeave_CalculationSnapshot", "([CalculationSnapshotVersion] IS NULL AND [CalculationSnapshotJson] IS NULL) OR ([CalculationSnapshotVersion] IS NOT NULL AND [CalculationSnapshotVersion] > 0 AND [CalculationSnapshotJson] IS NOT NULL AND ISJSON([CalculationSnapshotJson]) = 1)");
        });
        builder.Property(item => item.RequestedStartTime).HasColumnType("time(0)");
        builder.Property(item => item.RequestedEndTime).HasColumnType("time(0)");
        builder.Property(item => item.CalculationSnapshotJson).HasColumnType("nvarchar(max)");
        builder.HasKey(item => item.LeaveId);
        builder.Property(item => item.StartDate).HasColumnType("date").IsRequired();
        builder.Property(item => item.EndDate).HasColumnType("date").IsRequired();
        builder.Property(item => item.Days).IsRequired();
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.Property(item => item.Status).HasMaxLength(20).IsRequired();
        builder.Property(item => item.Remarks).HasMaxLength(2000);
        builder.HasIndex(item => new { item.EmployeeId, item.StartDate })
            .HasDatabaseName("IX_EmployeeLeave_EmployeeId_StartDate");
        builder.HasIndex(item => new { item.EmployeeId, item.EndDate })
            .HasDatabaseName("IX_EmployeeLeave_EmployeeId_EndDate");
        builder.HasOne(item => item.Employee).WithMany(employee => employee.Leaves)
            .HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.LeaveType).WithMany()
            .HasForeignKey(item => item.LeaveTypeId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class EmployeePerformanceConfiguration : IEntityTypeConfiguration<EmployeePerformance>
{
    public void Configure(EntityTypeBuilder<EmployeePerformance> builder)
    {
        builder.ToTable("EmployeePerformance");
        builder.HasKey(item => item.PerformanceRecordId);
        builder.Property(item => item.ReviewDate).HasColumnType("date").IsRequired();
        builder.Property(item => item.ReviewPeriodStart).HasColumnType("date");
        builder.Property(item => item.ReviewPeriodEnd).HasColumnType("date");
        builder.Property(item => item.Strengths).HasMaxLength(4000);
        builder.Property(item => item.AreasForImprovement).HasMaxLength(4000);
        builder.Property(item => item.Goals).HasMaxLength(4000);
        builder.Property(item => item.Remarks).HasMaxLength(2000);
        builder.HasIndex(item => new { item.EmployeeId, item.ReviewDate })
            .HasDatabaseName("IX_EmployeePerformance_EmployeeId_ReviewDate");
        builder.HasIndex(item => item.PerformanceRatingId);
        builder.HasIndex(item => item.ReviewerEmployeeId);
        builder.HasOne(item => item.Employee).WithMany(employee => employee.PerformanceRecords)
            .HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.PerformanceRating).WithMany()
            .HasForeignKey(item => item.PerformanceRatingId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(item => item.ReviewerEmployee).WithMany()
            .HasForeignKey(item => item.ReviewerEmployeeId).OnDelete(DeleteBehavior.NoAction);
    }
}
