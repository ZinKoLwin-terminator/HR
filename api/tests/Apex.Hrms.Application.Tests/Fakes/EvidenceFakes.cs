using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using Microsoft.Extensions.Logging;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>Evidence storage in memory. Refuses a key that is not 32 lower-case hex characters, like the real one.</summary>
internal sealed class FakeEvidenceStorage : IEvidenceStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public List<string> Deleted { get; } = [];

    public Exception? FailSaveWith { get; set; }

    public Exception? FailDeleteWith { get; set; }

    public Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken)
    {
        Checked(key);
        if (FailSaveWith is { } failure)
        {
            throw failure;
        }

        Assert.True(Files.TryAdd(key, content), "A file was stored twice under one key.");
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        Checked(key);
        return Task.FromResult<Stream?>(Files.TryGetValue(key, out var content) ? new MemoryStream(content, writable: false) : null);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        Checked(key);
        Deleted.Add(key);
        if (FailDeleteWith is { } failure)
        {
            throw failure;
        }

        Files.Remove(key);
        return Task.CompletedTask;
    }

    private static void Checked(string key) =>
        Assert.True(KpiAttachment.IsValidStorageKey(key), "The application passed a key that is not 32 lower-case hex characters.");
}

/// <summary>Collects what the handlers log, so a test can check the level and that no personal data is in it.</summary>
internal sealed class LogCapture
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];
}

internal sealed class CapturingLogger<T>(LogCapture capture) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        capture.Entries.Add((logLevel, formatter(state, exception)));
}

/// <summary>The handler calls of the evidence tests.</summary>
internal static class EvidenceFixtures
{
    public static Task<Result<EvidenceUploadResult>> UploadEvidence(this TestHost host, ScorecardItemDto item, byte[] content, string? fileName = "evidence.pdf", string? rowVersion = null, ScorecardDetailDto? card = null) =>
        host.Get<IUploadEvidenceHandler>().HandleAsync(
            item.Id,
            new EvidenceUploadRequest(rowVersion ?? (card is null ? null : Requests.Version(card.RowVersion)), fileName, content),
            default);

    public static Task<Result<EvidenceRemovalResult>> RemoveEvidence(this TestHost host, int attachmentId, string? rowVersion) =>
        host.Get<IRemoveEvidenceHandler>().HandleAsync(attachmentId, rowVersion, default);

    public static Task<Result<EvidenceDownload>> DownloadEvidence(this TestHost host, int attachmentId) =>
        host.Get<IDownloadEvidenceHandler>().HandleAsync(attachmentId, default);

    public static async Task<byte[]> ReadAllAsync(this EvidenceDownload download)
    {
        await using var stream = download.Content;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    /// <summary>The row version a screen would send: the scorecard's current one.</summary>
    public static string VersionOf(this TestHost host, ScorecardDetailDto card) => Requests.Version(host.Fresh(card).RowVersion);
}
