using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;

namespace L2ModernUpdater.Core;

public static class L2IniCodec
{
    private const int HeaderSize = 28;
    private const int TailSize = 20;
    private const int TailCrc32Offset = 12;
    private const int RsaBlockSize = 128;
    private const int RsaBlockBodySize = 124;
    private const string Header413 = "Lineage2Ver413";

    private const string ModernModulus =
        "75b4d6de5c016544068a1acf125869f43d2e09fc55b8b1e289556daf9b8757635593446288b3653da1ce91c87bb1a5c18f16323495c55d7d72c0890a83f69bfd1fd9434eb1c02f3e4679edfa43309319070129c267c85604d87bb65bae205de3707af1d2108881abb567c3b3d069ae67c3a4c6a3aa93d26413d4c66094ae2039";

    private const string ModernPublicExponent =
        "30b4c2d798d47086145c75063c8e841e719776e400291d7838d3e6c4405b504c6a07f8fca27f32b86643d2649d1d5f124cdd0bf272f0909dd7352fe10a77b34d831043d9ae541f8263c6fe3d1c14c2f04e43a7253a6dda9a8c1562cbd493c1b631a1957618ad5dfe5ca28553f746e2fc6f2db816c7db223ec91e955081c1de65";

    private const string ModernPrivateExponent = "1d";

    private const string LegacyModulus413 =
        "97df398472ddf737ef0a0cd17e8d172f0fef1661a38a8ae1d6e829bc1c6e4c3cfc19292dda9ef90175e46e7394a18850b6417d03be6eea274d3ed1dde5b5d7bde72cc0a0b71d03608655633881793a02c9a67d9ef2b45eb7c08d4be329083ce450e68f7867b6749314d40511d09bc5744551baa86a89dc38123dc1668fd72d83";

    private const string LegacyPrivateExponent413 = "35";

    private static readonly byte[] CachedHeader = BuildHeader();

    public static bool HasLineage2Ver413Header(ReadOnlySpan<byte> input)
    {
        if (input.Length < HeaderSize)
        {
            return false;
        }

        return input[..HeaderSize].SequenceEqual(CachedHeader);
    }

    public static byte[] Decode413(ReadOnlySpan<byte> input, bool preferLegacyRsa = true)
    {
        if (!HasLineage2Ver413Header(input))
        {
            throw new InvalidDataException("The file does not start with Lineage2Ver413.");
        }

        var encrypted = GetEncryptedBody(input);
        var first = preferLegacyRsa ? DecodeRsaProfile.Legacy413 : DecodeRsaProfile.Modern;
        var second = preferLegacyRsa ? DecodeRsaProfile.Modern : DecodeRsaProfile.Legacy413;

        if (TryDecode413(encrypted, first, out var decoded))
        {
            return decoded;
        }

        if (TryDecode413(encrypted, second, out decoded))
        {
            return decoded;
        }

        throw new InvalidDataException("Lineage2Ver413 decode failed.");
    }

    public static byte[] Encode413(ReadOnlySpan<byte> plainText)
    {
        var packed = PackZlib(plainText);
        var encrypted = RsaTransformWithPadding(packed, ModernModulus, ModernPublicExponent, addPadding: true);

        var output = new byte[HeaderSize + encrypted.Length + TailSize];
        CachedHeader.AsSpan().CopyTo(output);
        encrypted.CopyTo(output.AsSpan(HeaderSize));

        var checksum = Crc32.Compute(output.AsSpan(0, HeaderSize + encrypted.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(HeaderSize + encrypted.Length + TailCrc32Offset), checksum);

        return output;
    }

    public static byte[] Encode413Text(string plainText, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        return Encode413(encoding.GetBytes(plainText));
    }

    private static bool TryDecode413(ReadOnlySpan<byte> encrypted, DecodeRsaProfile profile, out byte[] decoded)
    {
        try
        {
            var packed = RsaTransformWithPadding(encrypted, profile.Modulus, profile.PrivateExponent, addPadding: false);
            decoded = UnpackZlib(packed);
            return true;
        }
        catch
        {
            decoded = [];
            return false;
        }
    }

    private static ReadOnlySpan<byte> GetEncryptedBody(ReadOnlySpan<byte> input)
    {
        var body = input[HeaderSize..];
        if (body.Length % RsaBlockSize == 0)
        {
            return body;
        }

        if (body.Length > TailSize && (body.Length - TailSize) % RsaBlockSize == 0)
        {
            return body[..^TailSize];
        }

        throw new InvalidDataException("Lineage2Ver413 encrypted body has an invalid size.");
    }

    private static byte[] PackZlib(ReadOnlySpan<byte> input)
    {
        using var compressedStream = new MemoryStream();
        using (var zlib = new ZLibStream(compressedStream, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(input);
        }

        var compressed = compressedStream.ToArray();
        var output = new byte[sizeof(uint) + compressed.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(output, checked((uint)input.Length));
        compressed.CopyTo(output.AsSpan(sizeof(uint)));
        return output;
    }

    private static byte[] UnpackZlib(ReadOnlySpan<byte> input)
    {
        if (input.Length < sizeof(uint))
        {
            throw new InvalidDataException("Packed Lineage2Ver413 data is too short.");
        }

        var expectedSize = BinaryPrimitives.ReadUInt32LittleEndian(input[..sizeof(uint)]);
        using var compressedStream = new MemoryStream(input[sizeof(uint)..].ToArray());
        using var zlib = new ZLibStream(compressedStream, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);

        if (output.Length != expectedSize)
        {
            throw new InvalidDataException("Unpacked Lineage2Ver413 size mismatch.");
        }

        return output.ToArray();
    }

    private static byte[] RsaTransformWithPadding(ReadOnlySpan<byte> input, string modulusHex, string exponentHex, bool addPadding)
    {
        var source = addPadding ? AddPadding(input) : input.ToArray();
        if (source.Length == 0 || source.Length % RsaBlockSize != 0)
        {
            throw new InvalidDataException("RSA data must be split into 128 byte blocks.");
        }

        var modulus = FromHex(modulusHex);
        var exponent = FromHex(exponentHex);
        var transformed = new byte[source.Length];

        for (var offset = 0; offset < source.Length; offset += RsaBlockSize)
        {
            var block = new BigInteger(source.AsSpan(offset, RsaBlockSize), isUnsigned: true, isBigEndian: true);
            var value = BigInteger.ModPow(block, exponent, modulus);
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);

            if (bytes.Length > RsaBlockSize)
            {
                throw new InvalidDataException("RSA block is larger than 128 bytes.");
            }

            bytes.CopyTo(transformed.AsSpan(offset + RsaBlockSize - bytes.Length));
        }

        return addPadding ? transformed : RemovePadding(transformed);
    }

    private static byte[] AddPadding(ReadOnlySpan<byte> input)
    {
        var blockCount = (input.Length + RsaBlockBodySize - 1) / RsaBlockBodySize;
        var output = new byte[blockCount * RsaBlockSize];
        var inputOffset = 0;

        for (var outputOffset = 0; inputOffset < input.Length; outputOffset += RsaBlockSize)
        {
            var chunkSize = Math.Min(input.Length - inputOffset, RsaBlockBodySize);
            output[outputOffset + 3] = checked((byte)chunkSize);

            var dataOffset = outputOffset + RsaBlockSize - AlignTo4Bytes(chunkSize);
            input.Slice(inputOffset, chunkSize).CopyTo(output.AsSpan(dataOffset));
            inputOffset += chunkSize;
        }

        return output;
    }

    private static byte[] RemovePadding(ReadOnlySpan<byte> input)
    {
        using var output = new MemoryStream(input.Length);
        for (var offset = 0; offset + RsaBlockSize <= input.Length; offset += RsaBlockSize)
        {
            var chunkSize = Math.Min((int)input[offset + 3], RsaBlockBodySize);
            var dataOffset = offset + RsaBlockSize - AlignTo4Bytes(chunkSize);
            if (dataOffset + chunkSize > input.Length)
            {
                throw new InvalidDataException("Invalid Lineage2Ver413 RSA padding.");
            }

            output.Write(input.Slice(dataOffset, chunkSize));
        }

        return output.ToArray();
    }

    private static int AlignTo4Bytes(int value)
    {
        return (value + 3) & ~3;
    }

    private static BigInteger FromHex(string hex)
    {
        return BigInteger.Parse("0" + hex, System.Globalization.NumberStyles.HexNumber);
    }

    private static byte[] BuildHeader()
    {
        var header = new byte[HeaderSize];
        for (var i = 0; i < Header413.Length; i++)
        {
            header[i * 2] = (byte)Header413[i];
        }

        return header;
    }

    private readonly record struct DecodeRsaProfile(string Modulus, string PrivateExponent)
    {
        public static DecodeRsaProfile Modern { get; } = new(ModernModulus, ModernPrivateExponent);

        public static DecodeRsaProfile Legacy413 { get; } = new(LegacyModulus413, LegacyPrivateExponent413);
    }

    private static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        public static uint Compute(ReadOnlySpan<byte> data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var value in data)
            {
                crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
            }

            return ~crc;
        }

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                var value = i;
                for (var bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) == 1 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                }

                table[i] = value;
            }

            return table;
        }
    }
}
