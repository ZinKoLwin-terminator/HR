using System.Buffers;
using System.Text;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Apex.Hrms.Api.Http;

/// <summary>
/// Reads the multipart body of an evidence upload without the framework form buffering (which would spool a large file to a temporary
/// file on disk): one file part named <c>file</c> and one text part named <c>rowVersion</c> (the scorecard row version). The whole request
/// is limited to the file limit plus a megabyte of multipart overhead, enforced three ways: the Content-Length when the client sends one,
/// the server request body limit, and a counting wrapper around the body that works whatever the server does. The file is read into memory
/// at most one byte past its limit, so an oversized file is refused (400 on <c>file</c>) without reading the rest. Nothing here logs the
/// file name.
/// </summary>
internal static class EvidenceUploadReader
{
    /// <summary>The largest request body accepted: the largest file and a megabyte for the multipart framing.</summary>
    public const long MaxRequestBytes = EvidenceFiles.MaxBytes + (1024 * 1024);

    private const string FileField = "file";
    private const string RowVersionField = "rowVersion";
    private const int MaxSections = 8;
    private const int MaxBoundaryLength = 70;
    private const int MaxTextFieldLength = 256;
    private const string OneFile = "Send exactly one file in the form field \"file\".";
    private const string NotMultipart = "Send the file as multipart/form-data.";
    private const string Malformed = "The upload could not be read. Send the file as multipart/form-data.";

    public static async Task<Result<EvidenceUploadRequest>> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType)
            || !string.Equals(mediaType.MediaType.Value, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return Refuse(NotMultipart);
        }

        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > MaxBoundaryLength)
        {
            return Refuse(Malformed);
        }

        if (request.ContentLength > MaxRequestBytes)
        {
            throw TooLarge();
        }

        var feature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = MaxRequestBytes;
        }

        request.Body = new CountingStream(request.Body, MaxRequestBytes);
        try
        {
            return await ReadSectionsAsync(
                new MultipartReader(boundary, request.Body)
                {
                    HeadersCountLimit = 8,
                    HeadersLengthLimit = 8 * 1024,
                    BodyLengthLimit = MaxRequestBytes,
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidDataException or (IOException and not BadHttpRequestException))
        {
            return Refuse(Malformed);
        }
    }

    private static async Task<Result<EvidenceUploadRequest>> ReadSectionsAsync(MultipartReader reader, CancellationToken cancellationToken)
    {
        string? rowVersion = null;
        string? fileName = null;
        byte[]? content = null;
        var sections = 0;
        while (await reader.ReadNextSectionAsync(cancellationToken) is { } section)
        {
            if (++sections > MaxSections || !ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition))
            {
                return Refuse(Malformed);
            }

            var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;
            if (disposition.FileName.HasValue || disposition.FileNameStar.HasValue)
            {
                if (name != FileField || content is not null)
                {
                    return Refuse(OneFile);
                }

                fileName = (disposition.FileNameStar.HasValue ? disposition.FileNameStar : HeaderUtilities.RemoveQuotes(disposition.FileName)).Value;
                content = await ReadFileAsync(section.Body, cancellationToken);
                if (content is null)
                {
                    return Refuse(EvidenceFiles.TooLargeMessage);
                }
            }
            else if (name == RowVersionField)
            {
                if (rowVersion is not null)
                {
                    return Refuse(Malformed);
                }

                rowVersion = await ReadTextAsync(section.Body, cancellationToken);
            }
            else if (name == FileField)
            {
                return Refuse(OneFile);
            }
        }

        return content is null
            ? Refuse(OneFile)
            : Result.Success(new EvidenceUploadRequest(rowVersion, fileName, content));
    }

    /// <summary>
    /// The file bytes, or null when it is larger than the limit (reading stops one byte past it). The section is read straight into one
    /// pooled working buffer of the limit plus one byte (no growing stream, no intermediate chunks), then copied once into an array of the
    /// exact size, which the handler keeps; the working buffer is cleared before it goes back to the pool, because it held an employee's
    /// file. So an upload costs one exact-size array, and repeated uploads reuse the working buffer instead of allocating 16 MB of
    /// growing streams each time.
    /// </summary>
    private static async Task<byte[]?> ReadFileAsync(Stream body, CancellationToken cancellationToken)
    {
        const int limit = (int)EvidenceFiles.MaxBytes + 1;
        var buffer = ArrayPool<byte>.Shared.Rent(limit);
        var filled = 0;
        try
        {
            int read;
            while (filled < limit && (read = await body.ReadAsync(buffer.AsMemory(filled, limit - filled), cancellationToken)) > 0)
            {
                filled += read;
            }

            return filled > EvidenceFiles.MaxBytes ? null : buffer.AsSpan(0, filled).ToArray();
        }
        finally
        {
            buffer.AsSpan(0, filled).Clear();
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>A short text field; anything longer is cut (a row version is 12 characters, so a longer one is invalid anyway).</summary>
    private static async Task<string> ReadTextAsync(Stream body, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var text = new char[MaxTextFieldLength];
        var length = await reader.ReadBlockAsync(text.AsMemory(), cancellationToken);
        return new string(text, 0, length);
    }

    private static Result<EvidenceUploadRequest> Refuse(string message) =>
        Result.Failure<EvidenceUploadRequest>(AppError.Validation(message, FileField, message));

    private static BadHttpRequestException TooLarge() =>
        new("The request body is too large.", StatusCodes.Status413PayloadTooLarge);

    /// <summary>Counts what is read and refuses (413) the byte that crosses the limit, whatever the server enforces.</summary>
    private sealed class CountingStream(Stream inner, long limit) : Stream
    {
        private long total;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Count(await inner.ReadAsync(buffer, cancellationToken));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int Count(int read)
        {
            total += read;
            return total > limit ? throw TooLarge() : read;
        }
    }
}
