using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SIAMIS.Application.Employees;

namespace SIAMIS.Infrastructure.Documents;

public sealed class PrivateDocumentOptions
{
    public string? Root { get; set; }
}
public sealed class PrivateDocumentStorage(IOptions<PrivateDocumentOptions> options, ILogger<PrivateDocumentStorage>? log = null) : IPrivateDocumentStorage
{
    public const long MaximumBytes=20L*1024*1024;
    public bool IsConfigured=>!string.IsNullOrWhiteSpace(options.Value.Root);
    private string Root()
    {
        if(!IsConfigured)throw new DocumentStorageException("storage_unavailable");
        var root=Path.GetFullPath(options.Value.Root!);
        for(var d=new DirectoryInfo(root);d is not null;d=d.Parent)
            if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)throw new DocumentStorageException("storage_unavailable");
        return root;
    }
    private string PathFor(string key)
    {
        if(!Regex.IsMatch(key,"\\A[a-f0-9]{32}\\.blob\\z"))throw new DocumentStorageException("storage_unavailable");
        var p=Path.Combine(Root(),key);
        if(File.Exists(p)&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new DocumentStorageException("storage_unavailable");
        return p;
    }
    public static string ValidateFileName(string name)
    {
        if(string.IsNullOrWhiteSpace(name)||name.Length>260||name!=name.Trim()||name.EndsWith('.')
            ||name.Any(c=>char.IsControl(c)||"/\\:<>\"|?*%".Contains(c)))throw new DocumentStorageException("validation");
        var stem=Path.GetFileNameWithoutExtension(name);
        if(Regex.IsMatch(stem,"\\A(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])\\z",RegexOptions.IgnoreCase))throw new DocumentStorageException("validation");
        return name.Normalize(NormalizationForm.FormC);
    }
    public static string ValidateSignature(string name,string declared,ReadOnlySpan<byte> first)
    {
        string extension=Path.GetExtension(name).ToLowerInvariant(),mime=declared.Trim().ToLowerInvariant();
        bool pdf=extension==".pdf"&&mime=="application/pdf"&&first.StartsWith("%PDF-"u8);
        bool jpeg=(extension==".jpg"||extension==".jpeg")&&mime=="image/jpeg"&&first.StartsWith(new byte[]{255,216,255});
        bool png=extension==".png"&&mime=="image/png"&&first.StartsWith(new byte[]{137,80,78,71,13,10,26,10});
        if(!pdf&&!jpeg&&!png)throw new DocumentStorageException("unsupported_type");
        return mime;
    }
    public async Task<StoredDocument> StoreAsync(Stream stream,string fileName,string declaredContentType,CancellationToken ct)
    {
        string? stage=null,final=null;
        try
        {
            fileName=ValidateFileName(fileName);
            var prefix=new byte[16];int prefixLength=0;
            while(prefixLength<prefix.Length){int n=await stream.ReadAsync(prefix.AsMemory(prefixLength),ct);if(n==0)break;prefixLength+=n;}
            if(prefixLength==0)throw new DocumentStorageException("validation");
            var mime=ValidateSignature(fileName,declaredContentType,prefix.AsSpan(0,prefixLength));
            var root=Root();Directory.CreateDirectory(root);Root();
            string key=Guid.NewGuid().ToString("N")+".blob";final=PathFor(key);stage=final+".stage";
            long size=prefixLength;using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);hash.AppendData(prefix,0,prefixLength);
            await using(var output=new FileStream(stage,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,FileOptions.Asynchronous))
            {
                await output.WriteAsync(prefix.AsMemory(0,prefixLength),ct);var buffer=new byte[65536];int n;
                while((n=await stream.ReadAsync(buffer,ct))>0)
                {
                    size+=n;if(size>MaximumBytes)throw new DocumentStorageException("too_large");
                    hash.AppendData(buffer,0,n);await output.WriteAsync(buffer.AsMemory(0,n),ct);
                }
                await output.FlushAsync(ct);
            }
            File.Move(stage,final,false);stage=null;
            return new(key,Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),size,mime,fileName);
        }
        catch(DocumentStorageException){throw;}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {throw new DocumentStorageException("storage_unavailable");}
        finally
        {
            if(stage is not null)
            {
                try{File.Delete(stage);}
                catch(Exception e) when(e is IOException or UnauthorizedAccessException)
                {log?.LogError("Failed private upload stage requires reconciliation.");}
            }
        }
    }
    public async Task<Stream> OpenVerifiedAsync(string key,string sha256,long size,CancellationToken ct)
    {
        try
        {
            if(size<=0||size>MaximumBytes)throw new DocumentStorageException("storage_unavailable");
            await using var file=new FileStream(PathFor(key),FileMode.Open,FileAccess.Read,FileShare.Read,65536,FileOptions.Asynchronous);
            using var memory=new MemoryStream();var buffer=new byte[65536];int n;
            while((n=await file.ReadAsync(buffer,ct))>0)
            {if(memory.Length+n>MaximumBytes)throw new DocumentStorageException("storage_unavailable");await memory.WriteAsync(buffer.AsMemory(0,n),ct);}
            var bytes=memory.ToArray();
            if(bytes.LongLength!=size||!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)),sha256,StringComparison.OrdinalIgnoreCase))throw new DocumentStorageException("storage_unavailable");
            return new MemoryStream(bytes,false); // Return only the exact verified snapshot, not a file that can change after hashing.
        }
        catch(DocumentStorageException){throw;}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {throw new DocumentStorageException("storage_unavailable");}
    }
    public Task RemoveFailedWriteAsync(string key,CancellationToken ct)
    {
        try{File.Delete(PathFor(key));return Task.CompletedTask;}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){throw new DocumentStorageException("storage_unavailable");}
    }
}
