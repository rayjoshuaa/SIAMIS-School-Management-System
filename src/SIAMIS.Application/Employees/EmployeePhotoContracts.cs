namespace SIAMIS.Application.Employees;

public sealed record EmployeePhotoDto(Guid? Version, bool HasPhoto, int? Width, int? Height, long? SizeBytes, string? ContentType);
public interface IEmployeePhotoStorage : IPrivateDocumentStorage { }
public interface IEmployeePhotoService
{
    Task<ServiceResult<EmployeePhotoDto>> GetAsync(Guid employeeId, CancellationToken ct);
    Task<ServiceResult<PrivateDocumentContent>> ReadAsync(Guid employeeId, Guid version, CancellationToken ct);
    Task<ServiceResult<EmployeePhotoDto>> UploadAsync(Guid employeeId, Guid? expectedVersion, Stream bytes, string fileName, CancellationToken ct);
    Task<ServiceResult<EmployeePhotoDto>> RemoveAsync(Guid employeeId, Guid expectedVersion, CancellationToken ct);
}
