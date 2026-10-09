using SkiaSharp;

namespace SIAMIS.Infrastructure.Documents;
public sealed record DecodedPhoto(byte[] Bytes, string ContentType, string FileName, int Width, int Height);
public static class EmployeePhotoDecoder
{
    public const int MaximumBytes = 5 * 1024 * 1024;
    // Bound simultaneously allocated decoder buffers across requests.
    private static readonly SemaphoreSlim DecodeSlots = new(2);
    public static async Task<DecodedPhoto> DecodeAsync(Stream input, string fileName, CancellationToken ct)
    {
        PrivateDocumentStorage.ValidateFileName(fileName);
        using var memory = new MemoryStream();
        var buffer = new byte[65536]; int count;
        while ((count = await input.ReadAsync(buffer, ct)) != 0)
        {
            if (memory.Length + count > MaximumBytes) throw new SIAMIS.Application.Employees.DocumentStorageException("too_large");
            await memory.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        var bytes = memory.ToArray();
        await DecodeSlots.WaitAsync(ct);
        try
        {
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png))
                throw new SIAMIS.Application.Employees.DocumentStorageException("unsupported_type");
            ValidateContainer(bytes, codec.EncodedFormat);
            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0 || info.Width > 4096 || info.Height > 4096)
                throw new SIAMIS.Application.Employees.DocumentStorageException("dimensions");
            // Require a complete actual decode; truncated files are never accepted as partial images.
            using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
                throw new SIAMIS.Application.Employees.DocumentStorageException("validation");
            ct.ThrowIfCancellationRequested();
            bool png = codec.EncodedFormat == SKEncodedImageFormat.Png;
            return new(bytes, png ? "image/png" : "image/jpeg", png ? "employee-photo.png" : "employee-photo.jpg", info.Width, info.Height);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        { throw new SIAMIS.Application.Employees.DocumentStorageException("validation"); }
        finally { DecodeSlots.Release(); }
    }
    private static void ValidateContainer(byte[] bytes, SKEncodedImageFormat format)
    {
        // Decoders may tolerate missing trailers. Require complete framing as well as pixel decoding.
        if (format == SKEncodedImageFormat.Jpeg)
        {
            if (bytes.Length < 4 || bytes[^2] != 0xff || bytes[^1] != 0xd9) Invalid();
            return;
        }
        int offset = 8;
        while (offset <= bytes.Length - 12)
        {
            uint length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            if (length > (uint)(bytes.Length - offset - 12)) Invalid();
            int payload = (int)length;
            var typeAndData = bytes.AsSpan(offset + 4, payload + 4);
            uint crc = uint.MaxValue;
            foreach (var b in typeAndData)
            {
                crc ^= b;
                for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
            }
            if (~crc != System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8 + payload, 4))) Invalid();
            var type = bytes.AsSpan(offset + 4, 4);
            if (type.SequenceEqual("acTL"u8)) Invalid(); // A profile photo is a still image.
            offset += payload + 12;
            if (type.SequenceEqual("IEND"u8))
            {
                if (payload != 0 || offset != bytes.Length) Invalid();
                return;
            }
        }
        Invalid();
    }
    private static void Invalid() => throw new SIAMIS.Application.Employees.DocumentStorageException("validation");
}
