using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SIAMIS.Application.Employees;

namespace SIAMIS.Infrastructure.Documents;
public sealed class EmployeePhotoStorageOptions { public string? Root { get; set; } }
public sealed class EmployeePhotoStorage(IOptions<EmployeePhotoStorageOptions> options, ILogger<PrivateDocumentStorage> log) : IEmployeePhotoStorage
{
    private readonly PrivateDocumentStorage storage = new(Options.Create(new PrivateDocumentOptions { Root = options.Value.Root }), log);
    public bool IsConfigured => storage.IsConfigured;
    public Task<StoredDocument> StoreAsync(Stream stream, string fileName, string declaredContentType, CancellationToken ct) => storage.StoreAsync(stream, fileName, declaredContentType, ct);
    public Task<Stream> OpenVerifiedAsync(string key, string sha256, long size, CancellationToken ct) => storage.OpenVerifiedAsync(key, sha256, size, ct);
    public Task RemoveFailedWriteAsync(string key, CancellationToken ct) => storage.RemoveFailedWriteAsync(key, ct);
}
