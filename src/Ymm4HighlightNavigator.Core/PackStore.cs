using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ymm4HighlightNavigator.Core;

public static class PackStore
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("NAVFP001");
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 24,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    };

    public static void Save(string path, FeaturePack pack, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        pack.Validate();
        string target = Path.GetFullPath(path);
        if (File.Exists(target)) throw new IOException("An existing Pack must not be overwritten.");
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(pack, Options);
        if (payload.Length > FeatureFormat.MaxPayloadBytes) throw new InvalidDataException("Pack exceeds the v1 payload budget.");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string temp = target + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(Magic);
                Span<byte> count = stackalloc byte[8]; BinaryPrimitives.WriteInt64LittleEndian(count, payload.LongLength); file.Write(count);
                file.Write(SHA256.HashData(payload));
                using (var compressed = new BrotliStream(file, CompressionLevel.Optimal, leaveOpen: true)) compressed.Write(payload);
                file.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            _ = Load(temp);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temp, target, overwrite: false);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static FeaturePack Load(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length < 49 || file.Length > FeatureFormat.MaxPayloadBytes + 1024 * 1024)
            throw new InvalidDataException("Invalid encoded Pack size.");
        Span<byte> header = stackalloc byte[48]; file.ReadExactly(header);
        if (!header[..8].SequenceEqual(Magic)) throw new InvalidDataException("Unknown Pack format.");
        long length = BinaryPrimitives.ReadInt64LittleEndian(header.Slice(8, 8));
        if (length < 1 || length > FeatureFormat.MaxPayloadBytes) throw new InvalidDataException("Invalid expanded Pack size.");
        byte[] data = new byte[(int)length];
        using (var compressed = new BrotliStream(file, CompressionMode.Decompress, leaveOpen: true))
        {
            compressed.ReadExactly(data);
            if (compressed.ReadByte() != -1) throw new InvalidDataException("Expanded Pack exceeds declared size.");
        }
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(data), header.Slice(16, 32)))
            throw new InvalidDataException("Pack checksum mismatch.");
        var pack = JsonSerializer.Deserialize<FeaturePack>(data, Options) ?? throw new InvalidDataException("Empty Pack.");
        pack.Validate();
        return pack;
    }
}
