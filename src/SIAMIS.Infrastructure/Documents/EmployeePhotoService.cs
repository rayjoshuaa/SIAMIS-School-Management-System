using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Security;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Security;
using SIAMIS.Infrastructure.Services;

namespace SIAMIS.Infrastructure.Documents;
public sealed class EmployeePhotoService(SIAMISDbContext db, IEmployeePhotoStorage storage, ICurrentActor actor, ILogger<EmployeePhotoService> log) : IEmployeePhotoService
{
    private bool Allowed(bool write) => actor.UserId.HasValue && actor.HasCapability(write ? "Employee.Manage" : "Employee.Read");
    private static ServiceResult<T> Fail<T>(string code, string message) => ServiceResult<T>.Fail(code, message);
    private IQueryable<EmployeePhotoRevision> Head(Guid id) => db.Set<EmployeePhotoRevision>().Where(x => x.EmployeeId == id && x.IsCurrent);
    private static EmployeePhotoDto Dto(EmployeePhotoRevision? x) => new(x?.Id, x?.StorageKey is not null, x?.Width, x?.Height, x?.SizeBytes, x?.ContentType);
    public async Task<ServiceResult<EmployeePhotoDto>> GetAsync(Guid id, CancellationToken ct)
    {
        if (!Allowed(false)) return Fail<EmployeePhotoDto>("forbidden", "Employee read access is required.");
        if (!await db.Employees.AnyAsync(x => x.EmployeeId == id, ct)) return Fail<EmployeePhotoDto>("not_found", "Employee was not found.");
        try { return ServiceResult<EmployeePhotoDto>.Success(Dto(await Head(id).AsNoTracking().SingleOrDefaultAsync(ct))); }
        catch (SqlException) { return Fail<EmployeePhotoDto>("storage_unavailable", "Employee photo metadata is unavailable."); }
    }
    public async Task<ServiceResult<PrivateDocumentContent>> ReadAsync(Guid id, Guid version, CancellationToken ct)
    {
        if (!Allowed(false)) return Fail<PrivateDocumentContent>("forbidden", "Employee read access is required.");
        try
        {
            var head = await Head(id).AsNoTracking().SingleOrDefaultAsync(ct);
            if (head?.Id != version || head.StorageKey is null) return Fail<PrivateDocumentContent>("not_found", "Photo was not found.");
            var stream = await storage.OpenVerifiedAsync(head.StorageKey, head.Sha256!, head.SizeBytes!.Value, ct);
            // Never serve a version removed/replaced while private bytes were being verified.
            if (!await Head(id).AnyAsync(x => x.Id == version, ct)) { await stream.DisposeAsync(); return Fail<PrivateDocumentContent>("not_found", "Photo has changed."); }
            return ServiceResult<PrivateDocumentContent>.Success(new(stream, head.ContentType!, head.ContentType == "image/png" ? "employee-photo.png" : "employee-photo.jpg"));
        }
        catch (Exception e) when (e is DocumentStorageException or SqlException)
        { return Fail<PrivateDocumentContent>("storage_unavailable", "Employee photo is unavailable."); }
    }
    public async Task<ServiceResult<EmployeePhotoDto>> UploadAsync(Guid id, Guid? version, Stream bytes, string fileName, CancellationToken ct)
    {
        if (!Allowed(true)) return Fail<EmployeePhotoDto>("forbidden", "Employee management is required.");
        if (!storage.IsConfigured) return Fail<EmployeePhotoDto>("storage_unavailable", "Private employee photo storage is unavailable.");
        DecodedPhoto image;
        try { image = await EmployeePhotoDecoder.DecodeAsync(bytes, fileName, ct); }
        catch (DocumentStorageException e) { return Fail<EmployeePhotoDto>(e.Code, e.Code == "too_large" ? "Maximum photo size is 5 MiB." : e.Code == "dimensions" ? "Maximum decoded dimensions are 4096 × 4096 pixels." : "Provide a complete JPEG or PNG image with a safe filename."); }
        catch (Exception e) when (e is DllNotFoundException or TypeInitializationException)
        { return Fail<EmployeePhotoDto>("storage_unavailable", "Image validation is unavailable."); }
        return await WriteAsync(id, version, image, ct);
    }
    public Task<ServiceResult<EmployeePhotoDto>> RemoveAsync(Guid id, Guid version, CancellationToken ct)
        => WriteAsync(id, version, null, ct);
    private async Task<ServiceResult<EmployeePhotoDto>> WriteAsync(Guid id, Guid? version, DecodedPhoto? image, CancellationToken ct)
    {
        if (!Allowed(true)) return Fail<EmployeePhotoDto>("forbidden", "Employee management is required.");
        StoredDocument? stored = null; bool committed = false; Guid? attempted = null;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (await EmploymentIntegrity.LockAsync(db, id, ct) is null) return Fail<EmployeePhotoDto>("not_found", "Employee was not found.");
            var previous = await Head(id).SingleOrDefaultAsync(ct);
            if (previous?.Id != version) return Fail<EmployeePhotoDto>("conflict", "Photo changed. Refresh before trying again.");
            if (image is null && previous?.StorageKey is null) return Fail<EmployeePhotoDto>("conflict", "There is no current photograph to remove.");
            if (image is not null)
            {
                using var input = new MemoryStream(image.Bytes, false);
                stored = await storage.StoreAsync(input, image.FileName, image.ContentType, ct);
            }
            if (previous is not null)
            {
                previous.IsCurrent = false;
                // Release the filtered unique head index before inserting the next revision.
                await db.SaveChangesAsync(ct);
            }
            var next = new EmployeePhotoRevision { EmployeeId = id, ActorUserId = actor.UserId!.Value,
                Operation = image is null ? "Remove" : previous?.StorageKey is null ? "Upload" : "Replace",
                StorageKey = stored?.StorageKey, Sha256 = stored?.ContentSha256, SizeBytes = stored?.SizeBytes,
                ContentType = stored?.ContentType, Width = image?.Width, Height = image?.Height };
            attempted = next.Id;
            db.Add(next);
            db.Add(new SecurityAuditEvent { ActorUserId = actor.UserId, ResourceType = "EmployeePhoto", ResourceId = next.Id.ToString(), Operation = $"Photo{next.Operation};EmployeeId={id}" });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct); committed = true;
            return ServiceResult<EmployeePhotoDto>.Success(Dto(next));
        }
        catch (DbUpdateConcurrencyException) { return Fail<EmployeePhotoDto>("conflict", "Photo changed. Refresh before trying again."); }
        catch (SqlException e) when (e.Number == 1205) { return Fail<EmployeePhotoDto>("conflict", "Concurrent photo command; refresh and retry."); }
        catch (Exception e) when (e is DbUpdateException or SqlException or DocumentStorageException)
        { return Fail<EmployeePhotoDto>("storage_unavailable", "Photo could not be saved safely."); }
        finally
        {
            if (!committed)
            {
                try { await tx.RollbackAsync(CancellationToken.None); } catch (Exception e) when (e is SqlException or InvalidOperationException) { log.LogError("Photo rollback needs reconciliation for revision {Id}.", attempted); }
                db.ChangeTracker.Clear();
                if (stored is not null)
                {
                    bool remove = !attempted.HasValue;
                    try
                    {
                        await using var probe = new SIAMISDbContext(new DbContextOptionsBuilder<SIAMISDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
                        remove = !await probe.Set<EmployeePhotoRevision>().AnyAsync(x => x.Id == attempted, CancellationToken.None);
                    }
                    catch (Exception) { log.LogError("Uncertain photo commit needs reconciliation for revision {Id}.", attempted); }
                    if (remove)
                        try { await storage.RemoveFailedWriteAsync(stored.StorageKey, CancellationToken.None); }
                        catch (DocumentStorageException) { log.LogError("Photo orphan cleanup needs reconciliation for revision {Id}.", attempted); }
                }
            }
        }
    }
}
