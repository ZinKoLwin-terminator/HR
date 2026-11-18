using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Performance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Infrastructure.Evidence;

/// <summary>
/// Keeps KPI evidence files in a local folder (default <c>App_Data/kpi-evidence</c> under the content root), permanently. A file is
/// <c>&lt;key&gt;.bin</c>: the key is 32 lower-case hexadecimal characters, checked before any path is built, so no key can name another
/// folder, and the client's file name is never used. Existing files are never overwritten. Content is never logged. The folder holds HR
/// records: keep it outside any web root, restrict it to the API's account and back it up with the database.
/// </summary>
internal sealed class LocalEvidenceStorage : IEvidenceStorage
{
    private const string Extension = ".bin";

    private readonly string folder;

    public LocalEvidenceStorage(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        this.folder = Path.GetFullPath(folder);
    }

    public async Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var path = PathOf(key);
        Directory.CreateDirectory(folder);

        // CreateNew refuses an existing file, so what is deleted below is always a file this call created.
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
        var complete = false;
        try
        {
            await stream.WriteAsync(content, cancellationToken);
            complete = true;
        }
        finally
        {
            await stream.DisposeAsync();
            if (!complete)
            {
                File.Delete(path);
            }
        }
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathOf(key);
        try
        {
            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return Task.FromResult<Stream?>(stream);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            File.Delete(PathOf(key));
        }
        catch (DirectoryNotFoundException)
        {
            // The folder does not exist yet, so neither does the file.
        }

        return Task.CompletedTask;
    }

    private string PathOf(string key)
    {
        if (!KpiAttachment.IsValidStorageKey(key))
        {
            throw new ArgumentException("Invalid evidence key.", nameof(key));
        }

        var path = Path.GetFullPath(Path.Combine(folder, key + Extension));
        return string.Equals(Path.GetDirectoryName(path), folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.Ordinal)
            ? path
            : throw new ArgumentException("Invalid evidence key.", nameof(key));
    }
}

/// <summary>Registers the evidence folder: <c>Kpi:EvidenceFolder</c> (a relative path starts at the content root), or <c>App_Data/kpi-evidence</c> under it.</summary>
public static class EvidenceStorageExtensions
{
    public const string FolderKey = "Kpi:EvidenceFolder";

    public const string DefaultFolder = "App_Data/kpi-evidence";

    public static string ResolveFolder(IConfiguration configuration, string contentRootPath) =>
        ResolveFolder(configuration, FolderKey, DefaultFolder, contentRootPath);

    /// <summary>The folder configured under <paramref name="key"/>, or <paramref name="defaultFolder"/>; a relative path starts at the content root.</summary>
    internal static string ResolveFolder(IConfiguration configuration, string key, string defaultFolder, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var configured = configuration[key];
        return Path.GetFullPath(string.IsNullOrWhiteSpace(configured) ? defaultFolder : configured.Trim(), contentRootPath);
    }

    public static IServiceCollection AddEvidenceStorage(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        var folder = ResolveFolder(configuration, contentRootPath);
        services.Replace(ServiceDescriptor.Singleton<IEvidenceStorage>(_ => new LocalEvidenceStorage(folder)));
        return services;
    }
}
