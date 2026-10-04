using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Security;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Documents;
using SIAMIS.Infrastructure.Security;

// Explicit opt-in local integration harness. Fault injection exists only in this test executable.
internal static class D14DatabaseTests
{
    private sealed class Actor : ICurrentActor
    {
        public Guid? UserId {get;}=Guid.NewGuid(); public Guid? EmployeeId=>null; public string Operation=>"D14 fault verification";
        public bool HasCapability(string capability)=>capability is "HRDocuments.Read" or "HRDocuments.Manage";
    }
    private sealed class SaveFailure : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,InterceptionResult<int> result,CancellationToken cancellationToken=default)
            => throw new DbUpdateException("Synthetic D14 save failure.");
    }
    private sealed class CommitFailure : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,TransactionEventData eventData,InterceptionResult result,CancellationToken cancellationToken=default)
            => throw new DbUpdateException("Synthetic D14 commit failure.");
    }
    private sealed class CommittedResponseFailure : DbTransactionInterceptor
    {
        public override Task TransactionCommittedAsync(DbTransaction transaction,TransactionEndEventData eventData,CancellationToken cancellationToken=default)
            => Task.FromException(new DbUpdateException("Synthetic failure after successful database commit."));
    }
    public static async Task RunAsync()
    {
        int checks=0;
        void Check(bool pass,string label) {if(!pass)throw new InvalidOperationException(label);checks++;Console.WriteLine("PASS: "+label);}
        const string connection="Server=localhost;Database=SIAMIS;Trusted_Connection=True;TrustServerCertificate=True";
        var employee=Guid.Parse("433f2c1a-6222-494f-a64f-cd0c31126dc4");var actor=new Actor();
        var root=Path.Combine(AppContext.BaseDirectory,"d14-failure-"+Guid.NewGuid().ToString("N"));
        var options=Options.Create(new PrivateDocumentOptions{Root=root});var storage=new PrivateDocumentStorage(options);
        HrDocumentService Service(SIAMISDbContext db)=>new(db,storage,actor,NullLogger<HrDocumentService>.Instance);
        DbContextOptions<SIAMISDbContext> OptionsFor(params IInterceptor[] interceptors)=>new DbContextOptionsBuilder<SIAMISDbContext>().UseSqlServer(connection).AddInterceptors(interceptors).Options;
        var input=new HrDocumentInput("GeneralHRDocument",null,null,null,null,"Synthetic failure verification");
        Guid? fixture=null,committedFixture=null;string? key=null;
        await using var baseline=new SIAMISDbContext(OptionsFor());
        var beforeDocuments=await baseline.EmployeeDocuments.CountAsync();var beforeAudit=await baseline.Set<SecurityAuditEvent>().CountAsync();
        try
        {
            foreach(var interceptor in new IInterceptor[]{new SaveFailure(),new CommitFailure()})
            {
                await using var db=new SIAMISDbContext(OptionsFor(interceptor));
                var result=await Service(db).UploadAsync(employee,input,new MemoryStream("%PDF-1.7\nsynthetic"u8.ToArray()),"safe.pdf","application/pdf",default);
                Check(!result.IsSuccess && result.Failure!.Code=="storage_unavailable","D14 injected "+interceptor.GetType().Name+" returns safe failure");
                Check(!Directory.Exists(root)||Directory.GetFiles(root).Length==0,"D14 failed database/commit cleans physical object");
                Check(await baseline.EmployeeDocuments.CountAsync()==beforeDocuments && await baseline.Set<SecurityAuditEvent>().CountAsync()==beforeAudit,"D14 failed new upload rolls back metadata and audit");
            }
            await using(var db=new SIAMISDbContext(OptionsFor()))
            {
                var result=await Service(db).UploadAsync(employee,input,new MemoryStream("%PDF-1.7\nold"u8.ToArray()),"safe.pdf","application/pdf",default);
                Check(result.IsSuccess,"D14 fault-test predecessor created");fixture=result.Value!.EmployeeDocumentId;
            }
            var original=await baseline.EmployeeDocuments.AsNoTracking().SingleAsync(x=>x.EmployeeDocumentId==fixture);key=original.StorageKey;
            foreach(var interceptor in new IInterceptor[]{new SaveFailure(),new CommitFailure()})
            {
                await using var db=new SIAMISDbContext(OptionsFor(interceptor));
                var result=await Service(db).ReplaceAsync(fixture.Value,employee,original.Version,new MemoryStream("%PDF-1.7\nnew"u8.ToArray()),"new.pdf","application/pdf",default);
                Check(!result.IsSuccess,"D14 failed replacement returned safe failure");
                var current=await baseline.EmployeeDocuments.AsNoTracking().SingleAsync(x=>x.EmployeeDocumentId==fixture);
                Check(current.Version==original.Version && current.LifecycleStatus=="Active" && current.ContentSha256==original.ContentSha256,"D14 failed replacement preserves exact predecessor metadata");
                Check(Directory.GetFiles(root).Length==1 && File.ReadAllBytes(Path.Combine(root,key)).SequenceEqual("%PDF-1.7\nold"u8.ToArray()),"D14 failed replacement preserves predecessor bytes; no orphan");
            }
            await using(var db=new SIAMISDbContext(OptionsFor(new CommittedResponseFailure())))
            {
                var result=await Service(db).UploadAsync(employee,input,new MemoryStream("%PDF-1.7\ncommitted"u8.ToArray()),"committed.pdf","application/pdf",default);
                Check(!result.IsSuccess,"D14 simulated post-commit response failure reported safely");
                var committedDoc=await baseline.EmployeeDocuments.AsNoTracking().SingleAsync(x=>x.CreatedByUserId==actor.UserId && x.EmployeeDocumentId!=fixture);
                committedFixture=committedDoc.EmployeeDocumentId;
                Check(File.Exists(Path.Combine(root,committedDoc.StorageKey)),"D14 uncertain/successful commit never loses committed bytes during compensation");
            }
            // Existing regular file blocks directory creation: real filesystem failure, no injected production switches.
            options.Value.Root=Path.Combine(root,"blocked");await File.WriteAllTextAsync(options.Value.Root,"synthetic blocker");
            await using(var db=new SIAMISDbContext(OptionsFor()))
            {
                var result=await Service(db).UploadAsync(employee,input,new MemoryStream("%PDF-1.7"u8.ToArray()),"safe.pdf","application/pdf",default);
                Check(!result.IsSuccess && result.Failure!.Code=="storage_unavailable","D14 real storage write failure safe response");
                Check(await baseline.EmployeeDocuments.CountAsync()==beforeDocuments+2,"D14 failed storage write creates no metadata");
            }
        }
        finally
        {
            if(committedFixture.HasValue)
            {
                await baseline.Set<SecurityAuditEvent>().Where(x=>x.ResourceType=="EmployeeDocument" && x.ResourceId==committedFixture.ToString()).ExecuteDeleteAsync();
                await baseline.EmployeeDocuments.Where(x=>x.EmployeeDocumentId==committedFixture).ExecuteDeleteAsync();
            }
            if(fixture.HasValue)
            {
                await baseline.Set<SecurityAuditEvent>().Where(x=>x.ResourceType=="EmployeeDocument" && x.ResourceId==fixture.ToString()).ExecuteDeleteAsync();
                await baseline.EmployeeDocuments.Where(x=>x.EmployeeDocumentId==fixture).ExecuteDeleteAsync();
            }
            if(Directory.Exists(root)){foreach(var file in Directory.GetFiles(root))File.Delete(file);Directory.Delete(root);}
        }
        Check(await baseline.EmployeeDocuments.CountAsync()==beforeDocuments && await baseline.Set<SecurityAuditEvent>().CountAsync()==beforeAudit,"D14 exact database counts restored after fault fixtures");
        Check(!Directory.Exists(root),"D14 fault-fixture private storage removed");
        Console.WriteLine($"PASS: {checks} D14 local database/storage fault checks.");
    }
}
