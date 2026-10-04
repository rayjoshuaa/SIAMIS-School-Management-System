using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Application.Security;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Security;

public sealed class HrSecurityReadService(SIAMISDbContext db,IEmployeePayrollService payrolls) : IHrSecurityReadService
{
    public async Task<PagedResult<StaffOverviewDto>> StaffAsync(int page,int size,CancellationToken ct)
        => new(await db.Employees.AsNoTracking().OrderBy(e=>e.EmployeeNumber).ThenBy(e=>e.EmployeeId).Skip((page-1)*size).Take(size)
            .Select(e=>new StaffOverviewDto(e.EmployeeId,e.EmployeeNumber,e.FirstName,e.LastName,e.IsActive)).ToArrayAsync(ct),page,size,await db.Employees.CountAsync(ct));
    public Task<StaffOverviewDto?> ProfileAsync(Guid id,CancellationToken ct)
        => db.Employees.AsNoTracking().Where(e=>e.EmployeeId==id).Select(e=>new StaffOverviewDto(e.EmployeeId,e.EmployeeNumber,e.FirstName,e.LastName,e.IsActive)).SingleOrDefaultAsync(ct);
    public async Task<PagedResult<LeaveStatusOverviewDto>> LeaveStatusAsync(int page,int size,CancellationToken ct)
        => new(await db.EmployeeLeaves.AsNoTracking().OrderByDescending(l=>l.StartDate).ThenBy(l=>l.LeaveId).Skip((page-1)*size).Take(size)
            .Select(l=>new LeaveStatusOverviewDto(l.LeaveId,l.EmployeeId,l.StartDate,l.EndDate,l.Status,l.ChargeableMinutes)).ToArrayAsync(ct),page,size,await db.EmployeeLeaves.CountAsync(ct));
    public async Task<PagedResult<EmployeePayrollListItemDto>> OwnPayrollsAsync(Guid employee,int page,int size,CancellationToken ct)
    {
        // Filter approved/paid at the database, before pagination; no Draft/Calculated or cancelled exposure.
        var q=db.EmployeePayrolls.AsNoTracking().Where(p=>p.EmployeeId==employee&&(p.Status=="Approved"||p.Status=="Paid"));
        var ids=await q.OrderByDescending(p=>p.PayrollPeriod.StartDate).ThenBy(p=>p.EmployeePayrollId).Skip((page-1)*size).Take(size).Select(p=>p.EmployeePayrollId).ToArrayAsync(ct);
        var list=new List<EmployeePayrollListItemDto>();
        foreach(var id in ids)
        {
            var p=(await payrolls.GetPayrollAsync(id,ct)).Value;
            if(p is not null&&p.Payroll.EmployeeId==employee&&p.Payroll.Status is "Approved" or "Paid")list.Add(new(p.Payroll,p.Employee.EmployeeNumber,p.Employee.EmployeeName,p.PayrollPeriod.Code,p.PayrollPeriod.Name));
        }
        return new(list,page,size,await q.CountAsync(ct));
    }
}
