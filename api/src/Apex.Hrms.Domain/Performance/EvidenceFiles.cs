using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Performance;

/// <summary>What an evidence file really is, decided by its content.</summary>
public enum EvidenceKind
{
    Pdf,
    Png,
    Jpeg,
    Docx,
    Xlsx,
}

/// <summary>An accepted evidence file: the cleaned name to store and the content type the API serves it with.</summary>
public sealed record EvidenceFileInfo(string FileName, string ContentType, EvidenceKind Kind);

/// <summary>
/// The rules for evidence files on a scorecard item. A file is accepted by its CONTENT, never by the content type the client claims:
/// PDF (<c>%PDF-</c>), PNG, JPEG and Office Open XML (a ZIP that really holds a Word or an Excel document). The name's extension must agree
/// with the content, and the content type that is stored and served comes from what was detected. The client's name is cleaned and is only
/// ever shown (in the download's file name): it is never part of a path.
/// </summary>
public static class EvidenceFiles
{
    public const long MaxBytes = KpiAttachment.MaxSizeBytes;

    /// <summary>At most this many files on one scorecard item.</summary>
    public const int MaxPerItem = 5;

    public const string NotAllowedMessage = "Allowed evidence files are PDF, PNG, JPEG, Word (.docx) and Excel (.xlsx), up to 10 MB.";

    public const string EmptyMessage = "The file is empty. " + NotAllowedMessage;

    public const string TooLargeMessage = "The file is larger than 10 MB. " + NotAllowedMessage;

    public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const string FallbackStem = "evidence";

    /// <summary>An Office Open XML package is a small number of parts; more than this many entries is not a document.</summary>
    private const int MaxZipEntries = 10_000;

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    public static string ContentTypeOf(EvidenceKind kind) => kind switch
    {
        EvidenceKind.Pdf => "application/pdf",
        EvidenceKind.Png => "image/png",
        EvidenceKind.Jpeg => "image/jpeg",
        EvidenceKind.Docx => DocxContentType,
        EvidenceKind.Xlsx => XlsxContentType,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>True for a content type this module stores (what a download may be served as).</summary>
    public static bool IsStoredContentType(string? contentType) =>
        Enum.GetValues<EvidenceKind>().Any(kind => string.Equals(ContentTypeOf(kind), contentType, StringComparison.Ordinal));

    /// <summary>The extension a file of this kind gets when its client name is empty.</summary>
    public static string DefaultExtensionOf(EvidenceKind kind) => kind switch
    {
        EvidenceKind.Pdf => ".pdf",
        EvidenceKind.Png => ".png",
        EvidenceKind.Jpeg => ".jpg",
        EvidenceKind.Docx => ".docx",
        EvidenceKind.Xlsx => ".xlsx",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The kind an extension (with its dot, any case) claims, or null for an extension that is not allowed.</summary>
    public static EvidenceKind? KindOfExtension(string? extension) => extension?.ToLowerInvariant() switch
    {
        ".pdf" => EvidenceKind.Pdf,
        ".png" => EvidenceKind.Png,
        ".jpg" or ".jpeg" => EvidenceKind.Jpeg,
        ".docx" => EvidenceKind.Docx,
        ".xlsx" => EvidenceKind.Xlsx,
        _ => null,
    };

    /// <summary>
    /// What the content is, by its first bytes. A ZIP counts only when it is a real Word or Excel package (it has the package's content
    /// types part and the main part of a Word or an Excel document); any other ZIP is not evidence. Null for anything else.
    /// </summary>
    public static EvidenceKind? Detect(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.AsSpan().StartsWith(PdfSignature))
        {
            return EvidenceKind.Pdf;
        }

        if (content.AsSpan().StartsWith(PngSignature))
        {
            return EvidenceKind.Png;
        }

        if (content.AsSpan().StartsWith(JpegSignature))
        {
            return EvidenceKind.Jpeg;
        }

        return content.AsSpan().StartsWith(ZipSignature) ? DetectOfficePackage(content) : null;
    }

    /// <summary>
    /// Checks the file and returns what to store. Failures are Validation errors on <c>file</c>: empty, larger than 10 MB, or not an allowed
    /// type (the content decides; the name's extension must agree with it). A name that cleans to nothing becomes <c>evidence.&lt;ext&gt;</c>.
    /// </summary>
    public static Result<EvidenceFileInfo> Inspect(string? clientFileName, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
        {
            return Refuse(EmptyMessage);
        }

        if (content.LongLength > MaxBytes)
        {
            return Refuse(TooLargeMessage);
        }

        var kind = Detect(content);
        if (kind is null)
        {
            return Refuse(NotAllowedMessage);
        }

        var name = SanitizeFileName(clientFileName);
        if (name.Length == 0)
        {
            return Result.Success(Info(FallbackStem + DefaultExtensionOf(kind.Value), kind.Value));
        }

        var extension = Path.GetExtension(name);
        if (KindOfExtension(extension) != kind)
        {
            return Refuse(NotAllowedMessage);
        }

        return Result.Success(Info(Path.GetFileNameWithoutExtension(name).Length == 0 ? FallbackStem + extension : name, kind.Value));
    }

    /// <summary>
    /// Cleans a client file name for display: only the part after the last path separator (either kind, on any platform), without control
    /// characters, bidirectional overrides, line separators or the characters Windows forbids in names, trimmed, at most 260 characters
    /// with the extension kept. Returns an empty string when nothing usable is left.
    /// </summary>
    public static string SanitizeFileName(string? clientFileName)
    {
        if (string.IsNullOrWhiteSpace(clientFileName))
        {
            return string.Empty;
        }

        var lastSeparator = clientFileName.LastIndexOfAny(['/', '\\']);
        var name = lastSeparator >= 0 ? clientFileName[(lastSeparator + 1)..] : clientFileName;
        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            if (!IsDropped(character))
            {
                builder.Append(character);
            }
        }

        var cleaned = builder.ToString().Trim();
        if (cleaned.Length == 0 || cleaned.All(character => character == '.'))
        {
            return string.Empty;
        }

        return cleaned.Length <= KpiAttachment.FileNameMaxLength ? cleaned : Shorten(cleaned);
    }

    private static string Shorten(string name)
    {
        var extension = Path.GetExtension(name);
        if (extension.Length is 0 or > 16)
        {
            extension = string.Empty;
        }

        var stem = name[..(KpiAttachment.FileNameMaxLength - extension.Length)];
        if (char.IsHighSurrogate(stem[^1]))
        {
            stem = stem[..^1];
        }

        return stem.TrimEnd() + extension;
    }

    /// <summary>Control characters, the characters Windows forbids in names, separators, line and paragraph separators (U+2028, U+2029), bidirectional marks and overrides (U+200E, U+200F, U+202A to U+202E, U+2066 to U+2069) and the byte order mark (U+FEFF).</summary>
    private static bool IsDropped(char character) =>
        char.IsControl(character)
        || character is (char)0x3C or (char)0x3E or (char)0x3A or (char)0x22 or (char)0x7C or (char)0x3F or (char)0x2A or (char)0x2F or (char)0x5C
        || character is (char)0x2028 or (char)0x2029 or (char)0x200E or (char)0x200F or (char)0xFEFF
        || character is (>= (char)0x202A and <= (char)0x202E) or (>= (char)0x2066 and <= (char)0x2069);

    private static EvidenceFileInfo Info(string fileName, EvidenceKind kind) => new(fileName, ContentTypeOf(kind), kind);

    private static Result<EvidenceFileInfo> Refuse(string message) =>
        Result.Failure<EvidenceFileInfo>(AppError.Validation(message, "file", message));

    private static EvidenceKind? DetectOfficePackage(byte[] content)
    {
        if (!HasPlausibleEntryCount(content))
        {
            return null;
        }

        try
        {
            using var archive = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
            if (archive.GetEntry("[Content_Types].xml") is null)
            {
                return null;
            }

            if (archive.GetEntry("word/document.xml") is not null)
            {
                return EvidenceKind.Docx;
            }

            return archive.GetEntry("xl/workbook.xml") is not null ? EvidenceKind.Xlsx : null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>Reads the entry count from the end-of-central-directory record before the archive is opened, so a package full of entries is never loaded.</summary>
    private static bool HasPlausibleEntryCount(byte[] content)
    {
        const int endRecordLength = 22;
        var searchFrom = content.Length - endRecordLength;
        var searchTo = Math.Max(0, searchFrom - ushort.MaxValue);
        for (var position = searchFrom; position >= searchTo; position--)
        {
            if (content[position] == 0x50 && content[position + 1] == 0x4B && content[position + 2] == 0x05 && content[position + 3] == 0x06)
            {
                var entries = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(position + 10, 2));
                return entries is > 0 and <= MaxZipEntries;
            }
        }

        return false;
    }
}
