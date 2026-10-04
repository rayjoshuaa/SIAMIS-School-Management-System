using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Security;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Documents;

internal static class D14DocumentTests
{
    public static void Run(Action<bool,string> check) => RunAsync(check).GetAwaiter().GetResult();
    private static async Task RunAsync(Action<bool,string> check)
    {
        foreach(var role in SecurityCapabilities.Roles.Keys)
        {
            var caps=SecurityCapabilities.ForRoles([role]);
            check(caps.Contains("HRDocuments.Read")== (role=="HRAdmin"),"D14 explicit Read role "+role);
            check(caps.Contains("HRDocuments.Manage")== (role=="HRAdmin"),"D14 explicit Manage role "+role);
        }
        check(SecurityCapabilities.ForRoles(["SystemAdmin","HRAdmin"]).Contains("HRDocuments.Manage"),"D14 combined roles explicitly grant documents");
        check(SecurityCapabilities.ForRoles(["SystemAdmin"]).Contains("Payroll.Manage"),"D14 unrelated SystemAdmin payroll grant retained");
        foreach(var name in new[]{"../a.pdf","C:\\a.pdf","a/b.pdf","a\\b.pdf","CON.pdf","a\r\n.pdf"," a.pdf","a%.pdf","a:stream.pdf",new string('a',261)+".pdf"})
        {
            bool rejected=false;try{PrivateDocumentStorage.ValidateFileName(name);}catch(DocumentStorageException){rejected=true;}
            check(rejected,"D14 unsafe filename rejected");
        }
        foreach(var tuple in new[]{("safe.pdf","application/pdf","%PDF-1.7"u8.ToArray()),("safe.jpg","image/jpeg",new byte[]{255,216,255,1}), ("safe.png","image/png",new byte[]{137,80,78,71,13,10,26,10})})
            check(PrivateDocumentStorage.ValidateSignature(tuple.Item1,tuple.Item2,tuple.Item3)==tuple.Item2,"D14 signature "+tuple.Item2);
        foreach(var tuple in new[]{("safe.exe","application/pdf","%PDF-"u8.ToArray()),("safe.pdf","image/png","%PDF-"u8.ToArray()),("safe.pdf","application/pdf","not a pdf"u8.ToArray())})
        {
            bool rejected=false;try{PrivateDocumentStorage.ValidateSignature(tuple.Item1,tuple.Item2,tuple.Item3);}catch(DocumentStorageException){rejected=true;}
            check(rejected,"D14 type/signature mismatch rejected");
        }
        var root=Path.Combine(AppContext.BaseDirectory,"d14-private-"+Guid.NewGuid().ToString("N"));
        var storage=new PrivateDocumentStorage(Options.Create(new PrivateDocumentOptions{Root=root}));
        var bytes="%PDF-1.7\nsynthetic D14 content"u8.ToArray();
        try
        {
            var stored=await storage.StoreAsync(new MemoryStream(bytes),"safe.pdf","application/pdf",default);
            check(stored.FileName=="safe.pdf" && !stored.StorageKey.Contains("safe"),"D14 filename never used as key");
            check(stored.SizeBytes==bytes.Length && stored.ContentSha256==Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),"D14 authoritative hash and size");
            await using(var result=await storage.OpenVerifiedAsync(stored.StorageKey,stored.ContentSha256,stored.SizeBytes,default))
            {using var buffer=new MemoryStream();await result.CopyToAsync(buffer);check(buffer.ToArray().SequenceEqual(bytes),"D14 verified exact bytes");}
            await File.WriteAllBytesAsync(Path.Combine(root,stored.StorageKey),"tampered"u8.ToArray());
            bool rejected=false;try{await storage.OpenVerifiedAsync(stored.StorageKey,stored.ContentSha256,stored.SizeBytes,default);}catch(DocumentStorageException){rejected=true;}
            check(rejected,"D14 tampering rejected");
            await storage.RemoveFailedWriteAsync(stored.StorageKey,default);
            rejected=false;try{await storage.OpenVerifiedAsync(stored.StorageKey,stored.ContentSha256,stored.SizeBytes,default);}catch(DocumentStorageException){rejected=true;}
            check(rejected,"D14 missing content rejected");
            rejected=false;try{await storage.OpenVerifiedAsync("../escape",stored.ContentSha256,stored.SizeBytes,default);}catch(DocumentStorageException){rejected=true;}
            check(rejected,"D14 key traversal rejected");
            var oversized=new byte[PrivateDocumentStorage.MaximumBytes+1];bytes.CopyTo(oversized,0);
            rejected=false;try{await storage.StoreAsync(new MemoryStream(oversized),"safe.pdf","application/pdf",default);}catch(DocumentStorageException e){rejected=e.Code=="too_large";}
            check(rejected && Directory.GetFiles(root).Length==0,"D14 streaming size failure cleans stage");
            var unavailable=new PrivateDocumentStorage(Options.Create(new PrivateDocumentOptions()));
            check(!unavailable.IsConfigured,"D14 unconfigured provider fails closed");
        }
        finally
        {
            // Only this generated, verified test directory; never user storage.
            if(Directory.Exists(root)) {foreach(var f in Directory.GetFiles(root))File.Delete(f);Directory.Delete(root);}
        }
        using var db=new SIAMISDbContext(new DbContextOptionsBuilder<SIAMISDbContext>().UseSqlServer("Server=localhost;Database=SIAMIS;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var entity=db.Model.FindEntityType(typeof(EmployeeDocument))!;
        check(entity.FindProperty("Version")!.IsConcurrencyToken,"D14 optimistic document version");
        check(entity.GetForeignKeys().All(f=>f.DeleteBehavior==DeleteBehavior.NoAction),"D14 retained document FKs NoAction");
        check(entity.GetForeignKeys().Any(f=>f.Properties.Select(p=>p.Name).SequenceEqual(new[]{"EmployeeId","EmploymentRecordId"})),"D14 employment ownership FK");
        check(entity.GetForeignKeys().Any(f=>f.Properties.Select(p=>p.Name).SequenceEqual(new[]{"EmployeeId","LeaveId","LeaveEvidenceId"})),"D14 existing evidence ownership FK");
        check(entity.GetIndexes().Any(i=>i.IsUnique && i.Properties.Count==1 && i.Properties[0].Name=="SupersedesDocumentId"),"D14 unique successor protects replacement");

        check(typeof(HrDocumentDto).GetProperty("StorageKey") is null,"D14 DTO hides storage keys");
        check(HrDocumentService.Categories.Count==5,"D14 bounded categories");
    }
}
