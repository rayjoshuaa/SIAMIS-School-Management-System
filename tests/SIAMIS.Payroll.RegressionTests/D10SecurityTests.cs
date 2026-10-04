using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SIAMIS.Application.Security;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Security;

internal static class D10SecurityTests
{
    private sealed class Actor(Guid user,Guid employee) : ICurrentActor
    {
        public Guid? UserId=>user;public Guid? EmployeeId=>employee;public string Operation=>"PureTest";public bool HasCapability(string capability)=>false;
    }
    public static void Run(Action<bool,string> check)
    {
        check(SecurityCapabilities.Roles.Count==5,"D10 five stable security roles");
        foreach(var role in new[]{"HRAdmin","Management","Employee"})
        {
            var caps=SecurityCapabilities.ForRoles([role]);
            check(!caps.Contains("Payroll.Read")&&!caps.Contains("Payroll.Manage"),"D10 payroll independent of "+role);
            check(!caps.Contains("Security.Manage"),"D10 security administration independent of "+role);
        }
        foreach(var role in new[]{"PayrollAdmin","SystemAdmin"})
            check(SecurityCapabilities.ForRoles([role]).Contains("Payroll.Manage"),"D10 explicit payroll capability "+role);
        check(SecurityCapabilities.ForRoles(["HRAdmin","PayrollAdmin"]).Contains("Employee.Manage"),"D10 independent roles may be combined");
        check(SecurityCapabilities.ForRoles(["HRAdmin","PayrollAdmin"]).Contains("Payroll.Manage"),"D10 combined payroll explicit");
        check(SecurityCapabilities.ForRoles(["Teacher","Finance","Principal"]).Length==0,"D10 job titles never imply security roles");
        check(SecurityCapabilities.ForRoles(["Management"]).SequenceEqual(["MasterData.Read", "Reporting.Read"]),"D10 management minimized reporting and lookup data only");
        check(SecurityCapabilities.ForRoles(["Employee"]).SequenceEqual(["MasterData.Read", "SelfService"]),"D10 employee scoped capability and lookup data only");
        var u=new ApplicationUser{Id=Guid.NewGuid()};var hasher=new PasswordHasher<ApplicationUser>();const string pw="D10 pure hash verification only";
        var hash=hasher.HashPassword(u,pw);
        check(hash!=pw,"D10 framework hash not plaintext");
        check(hash!=hasher.HashPassword(u,pw),"D10 framework salted hashes");
        check(hasher.VerifyHashedPassword(u,hash,pw)!=PasswordVerificationResult.Failed,"D10 framework verifies password");
        check(hasher.VerifyHashedPassword(u,hash,"invalid")==PasswordVerificationResult.Failed,"D10 invalid password fails");
        check(typeof(SecurityUserDto).GetProperty("PasswordHash")==null&&typeof(SecurityUserDto).GetProperty("SecurityStamp")==null,"D10 safe user DTO");
        foreach(var type in new[]{typeof(LoginRequest),typeof(CreateUserRequest),typeof(UserRolesRequest),typeof(UserStatusRequest),typeof(ChangePasswordRequest)})
        {
            check(type.GetProperty("ActorUserId")==null,"D10 actor not caller selectable "+type.Name);
            try{JsonSerializer.Deserialize("{\"ActorUserId\":\"forged\"}",type);check(false,"D10 strict request "+type.Name);}
            catch(JsonException){check(true,"D10 strict request "+type.Name);}
        }
        var user=Guid.NewGuid();var employee=Guid.NewGuid();
        using var db=new SIAMISDbContext(new DbContextOptionsBuilder<SIAMISDbContext>().UseSqlServer("Server=localhost;Database=PureModelOnly").Options,new Actor(user,employee));
        var model=db.GetService<IDesignTimeModel>().Model;var identity=model.FindEntityType(typeof(ApplicationUser))!;
        check(identity.GetTableName()=="Users","D10 one Identity user table");
        check(identity.GetIndexes().Any(i=>i.IsUnique&&i.Properties.SingleOrDefault()?.Name=="EmployeeId"),"D10 employee identity unique");
        check(identity.GetForeignKeys().Single(f=>f.Properties.Any(p=>p.Name=="EmployeeId")).DeleteBehavior==DeleteBehavior.NoAction,"D10 login deletion cannot cascade employee");
        check(identity.GetIndexes().Any(i=>i.IsUnique&&i.Properties.Any(p=>p.Name=="NormalizedUserName")),"D10 normalized username unique");
        check(identity.GetIndexes().Any(i=>i.IsUnique&&i.Properties.Any(p=>p.Name=="NormalizedEmail")),"D10 supplied email unique");
        check(model.FindEntityType(typeof(IdentityRole<Guid>))!.GetSeedData().Count()==5,"D10 deterministic permanent roles");
        check(model.FindEntityType(typeof(SecurityAuditEvent))!.GetProperties().All(p=>!p.Name.Contains("Password")&&!p.Name.Contains("Token")),"D10 audit has no secret payload fields");
        var e=new AttendanceEvent{EmployeeId=employee};var action=new AttendanceReviewAction{EmployeeId=employee};var revision=new FinalizedAttendanceRevision{EmployeeId=employee};
        db.Add(e);db.Add(action);db.Add(revision);
        typeof(SIAMISDbContext).GetMethod("UpdateTimestamps",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(db,null);
        check(e.ActorId==user&&e.ActorId!=employee,"D10 event actor is security identity, not employee");
        check(action.ActorUserId==user&&action.Origin=="Authenticated","D10 new action authenticated origin");
        check(revision.ActorUserId==user,"D10 new frozen revision actor");
        check(db.ChangeTracker.Entries<SecurityAuditEvent>().Count()==3,"D10 atomic per-resource mutation audits");
    }
}
