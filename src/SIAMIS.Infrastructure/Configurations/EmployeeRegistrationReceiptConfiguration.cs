using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Employees;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class EmployeeRegistrationReceiptConfiguration : IEntityTypeConfiguration<EmployeeRegistrationReceipt>
{
    public void Configure(EntityTypeBuilder<EmployeeRegistrationReceipt> b)
    {
        b.ToTable("EmployeeRegistrationReceipts", t =>
        {
            t.HasTrigger("TR_EmployeeRegistrationReceipts_Permanent");
            t.UseSqlOutputClause(false);
            t.HasCheckConstraint("CK_EmployeeRegistrationReceipt_Operation", "[Operation] = N'Employee.Register.v1'");
            t.HasCheckConstraint("CK_EmployeeRegistrationReceipt_Identity", "[ActorUserId] <> '00000000-0000-0000-0000-000000000000' AND [RequestKey] <> '00000000-0000-0000-0000-000000000000' AND [ResultEmployeeId] <> '00000000-0000-0000-0000-000000000000'");
            t.HasCheckConstraint("CK_EmployeeRegistrationReceipt_Hash", "DATALENGTH([PayloadHash]) = 32");
            t.HasCheckConstraint("CK_EmployeeRegistrationReceipt_ReplayWindow", "[ReplayUntilUtc] > [CompletedAtUtc]");
            t.HasCheckConstraint("CK_EmployeeRegistrationReceipt_Response", "[ResponseJson] IS NULL OR ISJSON([ResponseJson]) = 1");
        });
        b.HasKey(x => new { x.ActorUserId, x.Operation, x.RequestKey });
        b.Property(x => x.Operation).HasMaxLength(40).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.PayloadHash).HasColumnType("varbinary(32)").IsRequired();
        b.Property(x => x.ResultEmployeeNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.CompletedAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.ReplayUntilUtc).HasColumnType("datetime2(7)");
        b.HasIndex(x => x.ResultEmployeeId).IsUnique();
        b.HasIndex(x => x.ReplayUntilUtc).HasFilter("[ResponseJson] IS NOT NULL");
    }
}
