using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.MasterData;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class EmploymentStatusService(SIAMISDbContext db) : IEmploymentStatusService
{
    public async Task<ServiceResult<EmploymentStatusDto>> SaveAsync(Guid? id, EmploymentStatusRequest request, CancellationToken ct)
    {
        if (!Validator.TryValidateObject(request, new ValidationContext(request), [], true) || string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<EmploymentStatusDto>.Fail("validation", "Valid Name, IsActive and IsTerminal are required; field lengths must respect the master-data limits.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // TPC-derived master data does not support FromSql on its DbSet.
        if (id.HasValue)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT [Id] FROM [EmploymentStatuses] WITH (UPDLOCK) WHERE [Id] = {id.Value}", ct);
        var item = id.HasValue ? await db.EmploymentStatuses.SingleOrDefaultAsync(x => x.Id == id.Value, ct) : new EmploymentStatus();
        if (item is null) return ServiceResult<EmploymentStatusDto>.Fail("not_found", "EmploymentStatus was not found.");
        if (id.HasValue && item.IsTerminal != request.IsTerminal && await db.EmploymentRecords.AnyAsync(x => x.EmploymentStatusId == id.Value, ct))
            return ServiceResult<EmploymentStatusDto>.Fail("conflict", "IsTerminal cannot change while this status is referenced by employment history. Create a different status instead.");
        var code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
        if (code is not null && await db.EmploymentStatuses.AnyAsync(x => x.Code == code && x.Id != item.Id, ct))
            return ServiceResult<EmploymentStatusDto>.Fail("conflict", "Code is already in use.");
        item.Code = code; item.Name = request.Name.Trim();
        item.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        item.IsActive = request.IsActive!.Value; item.IsTerminal = request.IsTerminal!.Value;
        if (!id.HasValue) db.EmploymentStatuses.Add(item);
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { return ServiceResult<EmploymentStatusDto>.Fail("conflict", "Code is already in use."); }
        return ServiceResult<EmploymentStatusDto>.Success(new(item.Id, item.Code, item.Name, item.Description, item.IsActive, item.IsTerminal));
    }
}
