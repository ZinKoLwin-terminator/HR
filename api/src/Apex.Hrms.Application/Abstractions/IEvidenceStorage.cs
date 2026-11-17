namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// Permanent storage of KPI evidence files (the content; the database keeps only the metadata). Unlike the Excel store nothing
/// expires: a file stays until its attachment is removed. Keys are server-generated, 32 lower-case hexadecimal characters
/// (<c>Guid.NewGuid().ToString("N")</c>); a key that is not exactly that is refused with an <see cref="ArgumentException"/> before
/// any file system access, so no key can leave the storage folder. A client file name is never a key or part of one. Content and
/// names are never logged.
/// </summary>
public interface IEvidenceStorage
{
    /// <summary>A new key for a file about to be stored.</summary>
    static string NewKey() => Guid.NewGuid().ToString("N");

    /// <summary>Stores the content under a key that is not used yet (an existing file is never overwritten).</summary>
    Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken);

    /// <summary>The content as a stream the caller disposes, or null when no file has this key.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Removes the file; nothing happens when it does not exist.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
