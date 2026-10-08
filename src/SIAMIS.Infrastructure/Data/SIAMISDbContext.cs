using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using SIAMIS.Infrastructure.Security;
using SIAMIS.Application.Security;
using SIAMIS.Domain.Common;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Configurations;

namespace SIAMIS.Infrastructure.Data;

public sealed class SIAMISDbContext(DbContextOptions<SIAMISDbContext> options, ICurrentActor? actor = null) : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<SIAMIS.Domain.Entities.Leave.WorkCalendar> WorkCalendars => Set<SIAMIS.Domain.Entities.Leave.WorkCalendar>();
    public DbSet<SIAMIS.Domain.Entities.Leave.EmployeeLeaveEntitlement> EmployeeLeaveEntitlements => Set<SIAMIS.Domain.Entities.Leave.EmployeeLeaveEntitlement>();
    public DbSet<SIAMIS.Domain.Entities.OrganizationProfile> OrganizationProfiles => Set<SIAMIS.Domain.Entities.OrganizationProfile>();
    public DbSet<EmployeePayslip> EmployeePayslips => Set<EmployeePayslip>();
    public DbSet<EmployeePitPaymentSchedule> EmployeePitPaymentSchedules => Set<EmployeePitPaymentSchedule>();
    public DbSet<EmployeePayrollPitResult> EmployeePayrollPitResults => Set<EmployeePayrollPitResult>();
    public DbSet<EmployeePitPaymentScheduleEntry> EmployeePitPaymentScheduleEntries => Set<EmployeePitPaymentScheduleEntry>();
    public DbSet<EmployeePitPaymentScheduleSelection> EmployeePitPaymentScheduleSelections => Set<EmployeePitPaymentScheduleSelection>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<EmploymentType> EmploymentTypes => Set<EmploymentType>();
    public DbSet<EmploymentStatus> EmploymentStatuses => Set<EmploymentStatus>();
    public DbSet<ContractType> ContractTypes => Set<ContractType>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<AddressType> AddressTypes => Set<AddressType>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<HiringSource> HiringSources => Set<HiringSource>();
    public DbSet<Nationality> Nationalities => Set<Nationality>();
    public DbSet<Gender> Genders => Set<Gender>();
    public DbSet<MaritalStatus> MaritalStatuses => Set<MaritalStatus>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<AttendanceStatus> AttendanceStatuses => Set<AttendanceStatus>();
    public DbSet<PayType> PayTypes => Set<PayType>();
    public DbSet<PayrollComponent> PayrollComponents => Set<PayrollComponent>();
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<PayrollRule> PayrollRules => Set<PayrollRule>();
    public DbSet<PayrollRuleTarget> PayrollRuleTargets => Set<PayrollRuleTarget>();
    public DbSet<PayrollSettings> PayrollSettings => Set<PayrollSettings>();
    public DbSet<EmployeePayroll> EmployeePayrolls => Set<EmployeePayroll>();
    public DbSet<EmployeePayrollStatutoryResult> EmployeePayrollStatutoryResults => Set<EmployeePayrollStatutoryResult>();
    public DbSet<EmployeePayrollSocialSecurityResult> EmployeePayrollSocialSecurityResults => Set<EmployeePayrollSocialSecurityResult>();
    public DbSet<EmployeePayrollLine> EmployeePayrollLines => Set<EmployeePayrollLine>();
    public DbSet<EmployeePayrollComponentAssignment> EmployeePayrollComponentAssignments => Set<EmployeePayrollComponentAssignment>();
    public DbSet<PerformanceRating> PerformanceRatings => Set<PerformanceRating>();

    public DbSet<StatutoryScheme> StatutorySchemes => Set<StatutoryScheme>();
    public DbSet<StatutoryPolicyVersion> StatutoryPolicyVersions => Set<StatutoryPolicyVersion>();
    public DbSet<SocialSecurityPolicyConfiguration> SocialSecurityPolicyConfigurations => Set<SocialSecurityPolicyConfiguration>();
    public DbSet<PitPolicyConfiguration> PitPolicyConfigurations => Set<PitPolicyConfiguration>();
    public DbSet<PitTaxBracket> PitTaxBrackets => Set<PitTaxBracket>();
    public DbSet<EmployeeStatutoryEnrollment> EmployeeStatutoryEnrollments => Set<EmployeeStatutoryEnrollment>();
    public DbSet<EmployeeTaxProfile> EmployeeTaxProfiles => Set<EmployeeTaxProfile>();
    public DbSet<EmployeeTaxDeclaration> EmployeeTaxDeclarations => Set<EmployeeTaxDeclaration>();
    public DbSet<EmployeeTaxDeclarationSelection> EmployeeTaxDeclarationSelections => Set<EmployeeTaxDeclarationSelection>();
    public DbSet<EmployeeTaxClaim> EmployeeTaxClaims => Set<EmployeeTaxClaim>();
    public DbSet<EmployeeTaxOpeningBalance> EmployeeTaxOpeningBalances => Set<EmployeeTaxOpeningBalance>();

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeContact> EmployeeContacts => Set<EmployeeContact>();
    public DbSet<EmployeeAddress> EmployeeAddresses => Set<EmployeeAddress>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<EmploymentRecord> EmploymentRecords => Set<EmploymentRecord>();
    public DbSet<EmployeeContract> EmployeeContracts => Set<EmployeeContract>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();
    public DbSet<EmployeeCompensation> EmployeeCompensations => Set<EmployeeCompensation>();
    public DbSet<EmployeeHistory> EmployeeHistory => Set<EmployeeHistory>();
    public DbSet<EmployeeAttendance> Attendance => Set<EmployeeAttendance>();
    public DbSet<AttendanceEvent> AttendanceEvents => Set<AttendanceEvent>();
    public DbSet<EmployeeClockSession> EmployeeClockSessions => Set<EmployeeClockSession>();
    public DbSet<EmployeeLeave> EmployeeLeaves => Set<EmployeeLeave>();
    public DbSet<EmployeePerformance> EmployeePerformanceRecords => Set<EmployeePerformance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        SecurityConfiguration.Configure(modelBuilder);
        LeaveFoundationConfiguration.Configure(modelBuilder);

        modelBuilder.Entity<MasterDataEntity>().UseTpcMappingStrategy();
        modelBuilder.Entity<MasterDataEntity>().HasKey(item => item.Id);
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<Department>("Departments"));
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<Designation>("Designations"));
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<EmploymentType>("EmploymentTypes"));
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<EmploymentStatus>("EmploymentStatuses"));
        modelBuilder.Entity<EmploymentStatus>().Property(x => x.IsTerminal).IsRequired().HasDefaultValue(false);
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<ContractType>("ContractTypes"));
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<Location>("Locations"));
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<AddressType>("AddressTypes"));
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<Country>("Countries"));
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<HiringSource>("HiringSources"));
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<Nationality>("Nationalities"));
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<Gender>("Genders"));
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<MaritalStatus>("MaritalStatuses"));
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<DocumentType>("DocumentTypes"));
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<LeaveType>("LeaveTypes"));
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<AttendanceStatus>("AttendanceStatuses"));
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<PayType>("PayTypes"));
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<PayrollComponent>("PayrollComponents"));
        modelBuilder.ApplyConfiguration(new MasterDataConfiguration<PerformanceRating>("PerformanceRatings"));
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SIAMISDbContext).Assembly);
        MasterDataSeeds.Configure(modelBuilder);
    }

    public override int SaveChanges()
    {
        return SaveChanges(true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(true, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void UpdateTimestamps()
    {
        AttributeActor();
        foreach (var entry in ChangeTracker.Entries<EmployeeClockSession>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified &&
                (entry.Properties.Any(p => p.IsModified && p.Metadata.Name != nameof(EmployeeClockSession.OutEventId))
                 || entry.Property(x => x.OutEventId).OriginalValue.HasValue || !entry.Entity.OutEventId.HasValue))
                throw new InvalidOperationException("Clock sessions permit only one append-only closure; opening provenance is immutable.");
        }
        if (ChangeTracker.Entries<AttendanceEvent>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Attendance events are immutable observation evidence.");
        if (ChangeTracker.Entries<AttendanceReviewAction>().Any(x => x.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<FinalizedAttendanceRevision>().Any(x => x.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<AttendanceReviewCase>().Any(x => x.State == EntityState.Deleted
                || x.State == EntityState.Modified && x.Properties.Any(p => p.IsModified && p.Metadata.Name != nameof(AttendanceReviewCase.State))))
            throw new InvalidOperationException("Attendance review history and finalized snapshots are immutable.");
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IHasTimestamps>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(entity => entity.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }
    }

    private void AttributeActor()
    {
        if(ChangeTracker.Entries<SecurityAuditEvent>().Any(x=>x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Security audit events are immutable.");
        if(actor?.UserId is not Guid userId)return;
        var changes=ChangeTracker.Entries().Where(x=>x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        foreach(var entry in changes)
        {
            if(entry.Entity is SecurityAuditEvent || entry.Metadata.ClrType.Namespace?.StartsWith("Microsoft.AspNetCore.Identity") == true || entry.Entity is ApplicationUser)continue;
            if(entry.State==EntityState.Added)
            {
                switch(entry.Entity)
                {
                    case AttendanceEvent x:x.ActorId=userId;break;
                    case AttendanceReviewCase x:x.ActorUserId=userId;break;
                    case AttendanceReviewAction x:x.ActorUserId=userId;x.Origin="Authenticated";break;
                    case FinalizedAttendanceRevision x:x.ActorUserId=userId;break;
                    case SIAMIS.Domain.Entities.Leave.EmployeeLeaveEvidenceEvent x:x.ActorId=userId;break;
                    case SIAMIS.Domain.Entities.Leave.EmployeeLeaveSandwichEvent x:x.ActorId=userId;break;
                }
            }
            var key=string.Join("/",entry.Metadata.FindPrimaryKey()!.Properties.Select(p=>entry.Property(p.Name).CurrentValue));
            var operation=$"{actor.Operation}:{entry.State}";
            Set<SecurityAuditEvent>().Add(new(){ActorUserId=userId,Operation=operation.Length>200?operation[..200]:operation,ResourceType=entry.Metadata.ClrType.Name,ResourceId=key.Length>100?key[..100]:key});
        }
    }
}
