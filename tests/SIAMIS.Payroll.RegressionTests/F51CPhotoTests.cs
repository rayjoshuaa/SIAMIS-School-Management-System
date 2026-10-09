using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using System.Reflection;
using SIAMIS.Application.Employees;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Documents;
using SIAMIS.Domain.Entities.Employees;
using SkiaSharp;

internal static class F51CPhotoTests
{
    internal sealed class FaultStorage : IEmployeePhotoStorage, IDisposable
    {
        public readonly string Root = Path.Combine(Path.GetTempPath(), "SIAMIS_F51C_" + Guid.NewGuid().ToString("N"));
        private readonly PrivateDocumentStorage storage;
        public bool FailStore;
        public bool FailCleanup;
        public bool Configured = true;
        public FaultStorage() { storage = new(Options.Create(new PrivateDocumentOptions { Root = Root })); }
        public bool IsConfigured => Configured;
        public Task<StoredDocument> StoreAsync(Stream s, string f, string t, CancellationToken c) => FailStore ? throw new DocumentStorageException("storage_unavailable") : storage.StoreAsync(s,f,t,c);
        public Task<Stream> OpenVerifiedAsync(string k,string h,long n,CancellationToken c) => storage.OpenVerifiedAsync(k,h,n,c);
        public Task RemoveFailedWriteAsync(string k,CancellationToken c) => FailCleanup ? throw new DocumentStorageException("storage_unavailable") : storage.RemoveFailedWriteAsync(k,c);
        public void Dispose()
        {
            var full = Path.GetFullPath(Root);
            if (!full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("SIAMIS_F51C_")) throw new InvalidOperationException("Unsafe fixture path.");
            if (Directory.Exists(full)) Directory.Delete(full,true);
        }
    }
    internal static byte[] Image(int width = 2, int height = 2, bool jpeg = false)
    {
        using var bitmap = new SKBitmap(width,height); bitmap.Erase(SKColors.BurlyWood);
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(jpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png,90);
        return data.ToArray();
    }
    public static async Task RunAsync(HttpClient admin,HttpClient hr,HttpClient payroll,HttpClient anonymous,HttpClient self,Guid employee,Guid other,FaultStorage storage,SIAMISDbContext db)
    {
        int count = 0;
        void Check(bool pass,string message) { if(!pass) throw new InvalidOperationException("FAIL: " + message); count++; Console.WriteLine("PASS: " + message); }
        string path = $"/api/employees/{employee}/photo";
        var production = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production", Args = [] });
        production.Configuration.Sources.Clear();
        production.Configuration.AddInMemoryCollection();
        var registration = typeof(SIAMIS.Api.Controllers.EmployeePhotosController).Assembly.GetType("SIAMIS.Api.EmployeePhotoRegistration")!
            .GetMethod("AddEmployeePhotos", BindingFlags.Public | BindingFlags.Static)!;
        registration.Invoke(null, new object[] { production });
        using (var services = production.Services.BuildServiceProvider())
            Check(!services.GetRequiredService<IEmployeePhotoStorage>().IsConfigured, "Production photo storage is unconfigured and fail-closed by default");
        production.Configuration["EmployeePhotos:Root"] = Path.Combine(production.Environment.ContentRootPath, "wwwroot", "photos");
        bool rejectedRoot = false;
        try { registration.Invoke(null, new object[] { production }); }
        catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejectedRoot = true; }
        Check(rejectedRoot, "public web-root photo storage configuration rejected");
        async Task<HttpResponseMessage> Upload(HttpClient client,byte[] bytes,Guid? version=null,string name="photo.png")
        {
            using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(bytes),"file",name);
            if(version.HasValue) form.Add(new StringContent(version.Value.ToString()),"expectedVersion");
            return await client.PutAsync(path,form);
        }
        async Task<Guid> Version(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetGuid();
        var png=Image(); var jpeg=Image(jpeg:true);
        Check(await db.Set<EmployeePhotoRevision>().CountAsync()==0,"isolated photo schema is available");
        Check((await admin.GetFromJsonAsync<JsonElement>(path)).GetProperty("hasPhoto").GetBoolean()==false,"missing photo returns explicit initials fallback metadata");
        foreach(var client in new[]{anonymous,payroll,self})
        {
            var denied=client==anonymous ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
            Check((await client.GetAsync(path)).StatusCode==denied,"photo metadata read requires Employee.Read");
            Check((await client.GetAsync(path+"/content?version="+Guid.NewGuid())).StatusCode==denied,"direct image read requires Employee.Read");
            Check((await Upload(client,png)).StatusCode==denied,"photo mutation requires Employee.Manage");
            Check((await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,path){Content=JsonContent.Create(new {expectedVersion=Guid.NewGuid()})})).StatusCode==denied,"photo removal requires Employee.Manage");
        }
        Check((await self.GetAsync($"/api/employees/{other}/photo")).StatusCode==HttpStatusCode.Forbidden,"self-service does not acquire even own general Employee.Read photo access");
        var csrf=hr.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN").Single(); hr.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Check((await Upload(hr,png)).StatusCode==HttpStatusCode.BadRequest,"photo upload requires CSRF");
        Check((await hr.SendAsync(new HttpRequestMessage(HttpMethod.Delete,path){Content=JsonContent.Create(new {expectedVersion=Guid.NewGuid()})})).StatusCode==HttpStatusCode.BadRequest,"photo removal requires CSRF");
        hr.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf);
        Check((await Upload(hr,"not an image"u8.ToArray())).StatusCode==HttpStatusCode.UnsupportedMediaType,"non-image content rejected regardless of filename");
        Check((await Upload(hr,png[..20])).StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnsupportedMediaType,"malformed PNG rejected");
        Check((await Upload(hr,png[..^15])).StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnsupportedMediaType,"truncated decoded image rejected");
        var corrupt = png.ToArray(); corrupt[^1] ^= 1;
        Check((await Upload(hr,corrupt)).StatusCode==HttpStatusCode.BadRequest,"invalid PNG checksum rejected");
        Check((await Upload(hr,jpeg[..^2],name:"photo.jpg")).StatusCode==HttpStatusCode.BadRequest,"truncated JPEG rejected");
        Check((await Upload(hr,new byte[EmployeePhotoDecoder.MaximumBytes+1])).StatusCode==HttpStatusCode.RequestEntityTooLarge,"over 5 MiB rejected");
        Check((await Upload(hr,Image(4097,1))).StatusCode==HttpStatusCode.BadRequest,"decoded width limit enforced");
        Check((await Upload(hr,Image(1,4097))).StatusCode==HttpStatusCode.BadRequest,"decoded height limit enforced");
        Check((await Upload(hr,png,name:"../photo.png")).StatusCode==HttpStatusCode.BadRequest,"unsafe filename rejected");
        var initial=await Upload(hr,png); Check(initial.StatusCode==HttpStatusCode.OK,"valid PNG upload succeeds"); var version=await Version(initial);
        var metadata=await initial.Content.ReadAsStringAsync(); Check(!metadata.Contains("storageKey",StringComparison.OrdinalIgnoreCase) && !metadata.Contains(storage.Root),"metadata exposes no storage locator");
        var content=await admin.GetAsync(path+"/content?version="+version);
        Check(content.StatusCode==HttpStatusCode.OK && (await content.Content.ReadAsByteArrayAsync()).SequenceEqual(png),"authorized content retrieval returns verified PNG");
        Check(content.Headers.CacheControl!.NoStore && content.Headers.GetValues("X-Content-Type-Options").Single()=="nosniff","private binary has no-store and nosniff headers");
        Check((await admin.GetAsync($"/api/employees/{other}/photo/content?version={version}")).StatusCode==HttpStatusCode.NotFound,"photo version is scoped to employee");
        var replace=await Upload(admin,jpeg,version,"photo.jpg"); Check(replace.StatusCode==HttpStatusCode.OK,"valid JPEG replacement succeeds"); var replaced=await Version(replace);
        Check((await admin.GetAsync(path+"/content?version="+version)).StatusCode==HttpStatusCode.NotFound,"superseded photo inaccessible through current endpoint");
        Check(await db.Set<EmployeePhotoRevision>().CountAsync(x=>x.EmployeeId==employee)==2 && Directory.GetFiles(storage.Root,"*.blob").Length==2,"old photo retained privately as immutable revision");
        var concurrent=await Task.WhenAll(Upload(hr,png,replaced),Upload(admin,png,replaced));
        Check(concurrent.Count(x=>x.StatusCode==HttpStatusCode.OK)==1 && concurrent.Count(x=>x.StatusCode==HttpStatusCode.Conflict)==1,"concurrent expected-version replacements have one winner");
        version=await Version(concurrent.Single(x=>x.IsSuccessStatusCode));
        var before=Directory.GetFiles(storage.Root,"*.blob").Length;
        storage.FailStore=true; Check((await Upload(hr,png,version)).StatusCode==HttpStatusCode.ServiceUnavailable,"storage failure safely rejected"); storage.FailStore=false;
        F51ANumberingTests.FailPhotoAfterWrite=true;
        Check((await Upload(hr,png,version)).StatusCode==HttpStatusCode.ServiceUnavailable,"database failure after writes safely rejected"); F51ANumberingTests.FailPhotoAfterWrite=false;
        Check(Directory.GetFiles(storage.Root,"*.blob").Length==before && (await admin.GetFromJsonAsync<JsonElement>(path)).GetProperty("version").GetGuid()==version,"failed replacement restores prior head and removes uncommitted bytes");
        storage.FailCleanup=true; F51ANumberingTests.FailPhotoAfterWrite=true;
        Check((await Upload(hr,png,version)).StatusCode==HttpStatusCode.ServiceUnavailable,"cleanup failure still fails the command safely");
        storage.FailCleanup=false; F51ANumberingTests.FailPhotoAfterWrite=false;
        Check((await admin.GetFromJsonAsync<JsonElement>(path)).GetProperty("version").GetGuid()==version && Directory.GetFiles(storage.Root,"*.blob").Length==before+1,"cleanup failure preserves authoritative head and leaves only a private reconciliation orphan");
        var referenced=await db.Set<EmployeePhotoRevision>().Where(x=>x.StorageKey!=null).Select(x=>x.StorageKey).ToListAsync();
        foreach(var orphan in Directory.GetFiles(storage.Root,"*.blob").Where(x=>!referenced.Contains(Path.GetFileName(x)))) await storage.RemoveFailedWriteAsync(Path.GetFileName(orphan),default);
        var row=await db.Set<EmployeePhotoRevision>().AsNoTracking().SingleAsync(x=>x.Id==version);
        await File.WriteAllBytesAsync(Path.Combine(storage.Root,row.StorageKey!),new byte[]{0});
        Check((await admin.GetAsync(path+"/content?version="+version)).StatusCode==HttpStatusCode.ServiceUnavailable,"tampered private bytes never served");
        var remove=await hr.SendAsync(new HttpRequestMessage(HttpMethod.Delete,path){Content=JsonContent.Create(new {expectedVersion=version})});
        var removed = await remove.Content.ReadFromJsonAsync<JsonElement>();
        Check(remove.StatusCode==HttpStatusCode.OK && !removed.GetProperty("hasPhoto").GetBoolean(),"removal creates initials fallback tombstone");
        Check((await admin.GetAsync(path+"/content?version="+version)).StatusCode==HttpStatusCode.NotFound,"removed photo cannot be retrieved");
        Check((await hr.SendAsync(new HttpRequestMessage(HttpMethod.Delete,path){Content=JsonContent.Create(new {expectedVersion=version})})).StatusCode==HttpStatusCode.Conflict,"stale removal rejected");
        Check(await db.Set<SIAMIS.Infrastructure.Security.SecurityAuditEvent>().CountAsync(x=>x.ResourceType=="EmployeePhoto") == 4,"upload replacement and removal have immutable authenticated audit events");
        var history=await db.Set<EmployeePhotoRevision>().SingleAsync(x=>x.Id==replaced); history.Width=3;
        bool immutable=false; try { await db.SaveChangesAsync(); } catch(InvalidOperationException) { immutable=true; } db.ChangeTracker.Clear();
        Check(immutable,"EF rejects rewriting photo provenance");
        storage.Configured=false;
        Check((await Upload(hr,png,removed.GetProperty("version").GetGuid())).StatusCode==HttpStatusCode.ServiceUnavailable,"unconfigured private storage fails closed"); storage.Configured=true;
        Check((await admin.GetAsync($"/api/employees/{employee}/documents")).StatusCode==HttpStatusCode.Forbidden,"SystemAdmin photo access does not broaden confidential documents");
        Console.WriteLine($"PASS: {count} F5.1C real HTTP/Identity/CSRF/SQL/private-storage assertions.");
    }
}
