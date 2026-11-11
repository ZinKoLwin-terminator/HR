using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Apex.Hrms.Application.Abstractions;

namespace Apex.Hrms.Infrastructure.Excel;

/// <summary>
/// Keeps uploaded workbooks in a local folder (default <c>App_Data/excel-uploads</c> under the content root) until they are
/// used or expire. A key is a GUID without dashes, so no key can leave the folder. Content is never logged.
/// </summary>
internal sealed class LocalFileStorage(string folder, TimeProvider time) : IFileStorage
{
    private sealed record Sidecar(DateTimeOffset ExpiresAt, Dictionary<string, string> Metadata);

    public async Task SaveAsync(string key, byte[] content, IReadOnlyDictionary<string, string> metadata, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(DataPath(key), content, cancellationToken);
        var sidecar = new Sidecar(time.GetUtcNow() + lifetime, new Dictionary<string, string>(metadata));
        await File.WriteAllTextAsync(MetaPath(key), JsonSerializer.Serialize(sidecar), cancellationToken);
    }

    public async Task<StoredFile?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        if (!File.Exists(DataPath(key)) || !File.Exists(MetaPath(key)))
        {
            return null;
        }

        var sidecar = JsonSerializer.Deserialize<Sidecar>(await File.ReadAllTextAsync(MetaPath(key), cancellationToken));
        if (sidecar is null || sidecar.ExpiresAt <= time.GetUtcNow())
        {
            await DeleteAsync(key, cancellationToken);
            return null;
        }

        return new StoredFile(await File.ReadAllBytesAsync(DataPath(key), cancellationToken), sidecar.Metadata);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        File.Delete(DataPath(key));
        File.Delete(MetaPath(key));
        return Task.CompletedTask;
    }

    public async Task PurgeExpiredAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var meta in Directory.EnumerateFiles(folder, "*.json"))
        {
            var key = Path.GetFileNameWithoutExtension(meta);
            if (!IsKey(key))
            {
                continue;
            }

            try
            {
                var sidecar = JsonSerializer.Deserialize<Sidecar>(await File.ReadAllTextAsync(meta, cancellationToken));
                if (sidecar is null || sidecar.ExpiresAt <= time.GetUtcNow())
                {
                    await DeleteAsync(key, cancellationToken);
                }
            }
            catch (JsonException)
            {
                await DeleteAsync(key, cancellationToken);
            }
            catch (IOException)
            {
                // Being written or removed by another request: leave it for the next purge.
            }
        }
    }

    private static bool IsKey(string key) => Guid.TryParseExact(key, "N", out _);

    private string DataPath(string key) => Path.Combine(folder, Checked(key) + ".xlsx");

    private string MetaPath(string key) => Path.Combine(folder, Checked(key) + ".json");

    private static string Checked(string key) =>
        IsKey(key) ? key : throw new ArgumentException("Invalid file key.", nameof(key));
}

/// <summary>Registers the upload folder under the content root of the host (the same App_Data convention as the development mail folder).</summary>
public static class ExcelStorageExtensions
{
    public static IServiceCollection AddExcelUploadFolder(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        var folder = configuration["Excel:StorageFolder"] is { Length: > 0 } configured
            ? configured
            : Path.Combine(contentRootPath, "App_Data", "excel-uploads");
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.Replace(
            services,
            ServiceDescriptor.Singleton<IFileStorage>(
                provider => new LocalFileStorage(folder, provider.GetRequiredService<TimeProvider>())));
        return services;
    }
}
