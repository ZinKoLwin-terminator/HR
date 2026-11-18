using Apex.Hrms.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Infrastructure.Evidence;

/// <summary>
/// Keeps grievance files in their own local folder, with exactly the rules of <see cref="LocalEvidenceStorage"/> (which it
/// delegates to): <c>&lt;key&gt;.bin</c>, keys checked before any path is built, never overwritten, content never logged. The folder
/// holds confidential HR records: keep it outside any web root, restrict it to the API's account and back it up with the database.
/// </summary>
internal sealed class LocalGrievanceFileStorage(string folder) : IGrievanceFileStorage
{
    private readonly LocalEvidenceStorage files = new(folder);

    public Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken) => files.SaveAsync(key, content, cancellationToken);

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken) => files.OpenReadAsync(key, cancellationToken);

    public Task DeleteAsync(string key, CancellationToken cancellationToken) => files.DeleteAsync(key, cancellationToken);
}

/// <summary>
/// Registers the grievance folder: <c>Grievance:FileFolder</c> (a relative path starts at the content root), or
/// <c>App_Data/grievance-files</c> under it. It must not be the KPI evidence folder.
/// </summary>
public static class GrievanceFileStorageExtensions
{
    public const string FolderKey = "Grievance:FileFolder";

    public const string DefaultFolder = "App_Data/grievance-files";

    public static string ResolveFolder(IConfiguration configuration, string contentRootPath) =>
        EvidenceStorageExtensions.ResolveFolder(configuration, FolderKey, DefaultFolder, contentRootPath);

    /// <exception cref="InvalidOperationException">The grievance folder is the KPI evidence folder.</exception>
    public static IServiceCollection AddGrievanceFileStorage(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        var folder = ResolveFolder(configuration, contentRootPath);
        var kpiFolder = EvidenceStorageExtensions.ResolveFolder(configuration, contentRootPath);
        if (string.Equals(
                Path.TrimEndingDirectorySeparator(folder),
                Path.TrimEndingDirectorySeparator(kpiFolder),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{FolderKey} must be a folder of its own, not the KPI evidence folder ({EvidenceStorageExtensions.FolderKey}).");
        }

        services.Replace(ServiceDescriptor.Singleton<IGrievanceFileStorage>(_ => new LocalGrievanceFileStorage(folder)));
        return services;
    }
}
