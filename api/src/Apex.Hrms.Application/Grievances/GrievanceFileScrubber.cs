using System.Buffers.Binary;
using System.IO.Compression;
using System.Xml.Linq;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Grievances;

/// <summary>
/// Removes the metadata that could identify the submitter of an ANONYMOUS case from a file before it is stored (security review M3), with
/// the base class library only:
/// <list type="bullet">
/// <item>DOCX and XLSX: <c>docProps/core.xml</c> and <c>docProps/app.xml</c> are emptied (author, last modified by, company, manager, dates,
/// template ...), <c>docProps/custom.xml</c> is dropped together with its relationship and content-type entry; every other part is copied.</item>
/// <item>JPEG: the APP1 (EXIF, XMP), APP13 (IPTC) and COM segments before the image data are dropped.</item>
/// <item>PNG: the tEXt, iTXt, zTXt, eXIf and tIME chunks are dropped.</item>
/// <item>PDF: cannot be cleaned safely without a PDF library, so it is stored as it is (the screens warn the submitter).</item>
/// </list>
/// A file whose structure cannot be followed is refused rather than stored with its metadata. The caller recomputes size and hash.
/// </summary>
internal static class GrievanceFileScrubber
{
    public const string CannotClean = "This file could not be cleaned of its hidden details (author, location, dates). Save it again, or as a PDF, and upload it again.";

    /// <summary>Uncompressed bytes a cleaned Office package may hold (a guard against decompression bombs).</summary>
    private const long MaxUnpackedBytes = 64L * 1024 * 1024;

    private const string CustomPart = "docProps/custom.xml";

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly string[] PngMetadataChunks = ["tEXt", "iTXt", "zTXt", "eXIf", "tIME"];

    /// <summary>The cleaned content, or a Validation failure on <c>file</c> when the file cannot be cleaned.</summary>
    public static Result<byte[]> Scrub(EvidenceKind kind, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            var cleaned = kind switch
            {
                EvidenceKind.Docx or EvidenceKind.Xlsx => ScrubOffice(content),
                EvidenceKind.Jpeg => ScrubJpeg(content),
                EvidenceKind.Png => ScrubPng(content),
                _ => content,
            };
            return cleaned is null || cleaned.Length == 0 || cleaned.LongLength > Domain.Grievances.GrievanceAttachment.MaxSizeBytes
                ? Refuse()
                : Result.Success(cleaned);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or System.Xml.XmlException or ArgumentException)
        {
            return Refuse();
        }
    }

    private static Result<byte[]> Refuse() => Result.Failure<byte[]>(AppError.Validation(CannotClean, "file", CannotClean));

    // ---- Office packages ----

    private static byte[]? ScrubOffice(byte[] content)
    {
        using var input = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
        using var output = new MemoryStream();
        long unpacked = 0;
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (string.Equals(name, CustomPart, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var data = ReadBounded(entry, ref unpacked);
                if (data is null)
                {
                    return null;
                }

                data = name.ToLowerInvariant() switch
                {
                    "docprops/core.xml" or "docprops/app.xml" => EmptyRoot(data),
                    "_rels/.rels" => WithoutCustomRelationship(data),
                    "[content_types].xml" => WithoutCustomOverride(data),
                    _ => data,
                };

                // A fixed time, so the package itself does not keep when or where it was cleaned either.
                var copy = archive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                copy.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var stream = copy.Open();
                stream.Write(data);
            }
        }

        return output.ToArray();
    }

    private static byte[]? ReadBounded(ZipArchiveEntry entry, ref long unpacked)
    {
        using var source = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = source.Read(chunk)) > 0)
        {
            unpacked += read;
            if (unpacked > MaxUnpackedBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>The same root element with nothing in it: every property is gone, the part stays valid.</summary>
    private static byte[] EmptyRoot(byte[] xml)
    {
        var document = Load(xml);
        document.Root!.RemoveNodes();
        foreach (var attribute in document.Root.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration).ToList())
        {
            attribute.Remove();
        }

        return Save(document);
    }

    private static byte[] WithoutCustomRelationship(byte[] xml)
    {
        var document = Load(xml);
        document.Root!.Elements()
            .Where(element => (string?)element.Attribute("Target") is { } target
                && target.TrimStart('/').Equals(CustomPart, StringComparison.OrdinalIgnoreCase))
            .Remove();
        return Save(document);
    }

    private static byte[] WithoutCustomOverride(byte[] xml)
    {
        var document = Load(xml);
        document.Root!.Elements()
            .Where(element => (string?)element.Attribute("PartName") is { } part
                && part.TrimStart('/').Equals(CustomPart, StringComparison.OrdinalIgnoreCase))
            .Remove();
        return Save(document);
    }

    private static XDocument Load(byte[] xml)
    {
        // No DTDs, no external entities: an Office part never has them.
        using var reader = System.Xml.XmlReader.Create(
            new MemoryStream(xml, writable: false),
            new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader);
    }

    private static byte[] Save(XDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, SaveOptions.DisableFormatting);
        return stream.ToArray();
    }

    // ---- JPEG ----

    /// <summary>Copies the segments before the image data except APP1, APP13 and COM, then the rest unchanged. Null when malformed.</summary>
    private static byte[]? ScrubJpeg(byte[] content)
    {
        if (content.Length < 4 || content[0] != 0xFF || content[1] != 0xD8)
        {
            return null;
        }

        using var output = new MemoryStream(content.Length);
        output.Write(content, 0, 2);
        var position = 2;
        while (position < content.Length)
        {
            if (content[position] != 0xFF)
            {
                return null;
            }

            // Fill bytes before a marker.
            while (position + 1 < content.Length && content[position + 1] == 0xFF)
            {
                position++;
            }

            if (position + 1 >= content.Length)
            {
                return null;
            }

            var marker = content[position + 1];
            if (marker == 0xDA || marker == 0xD9)
            {
                // Start of scan (or end of image): the image data follows; copy it all.
                output.Write(content, position, content.Length - position);
                return output.ToArray();
            }

            if (marker is 0x01 or (>= 0xD0 and <= 0xD7))
            {
                output.Write(content, position, 2);
                position += 2;
                continue;
            }

            if (position + 4 > content.Length)
            {
                return null;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(position + 2));
            if (length < 2 || position + 2 + length > content.Length)
            {
                return null;
            }

            var dropped = marker is 0xE1 or 0xED or 0xFE;
            if (!dropped)
            {
                output.Write(content, position, 2 + length);
            }

            position += 2 + length;
        }

        return null;
    }

    // ---- PNG ----

    /// <summary>Copies every chunk except the text, EXIF and time chunks, up to and including IEND. Null when malformed.</summary>
    private static byte[]? ScrubPng(byte[] content)
    {
        if (content.Length < PngSignature.Length || !content.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
        {
            return null;
        }

        using var output = new MemoryStream(content.Length);
        output.Write(PngSignature);
        var position = PngSignature.Length;
        while (position + 12 <= content.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(position));
            if (length > int.MaxValue || position + 12L + length > content.Length)
            {
                return null;
            }

            var type = System.Text.Encoding.ASCII.GetString(content, position + 4, 4);
            var total = 12 + (int)length;
            if (!PngMetadataChunks.Contains(type, StringComparer.Ordinal))
            {
                output.Write(content, position, total);
            }

            position += total;
            if (type == "IEND")
            {
                return output.ToArray();
            }
        }

        return null;
    }
}
