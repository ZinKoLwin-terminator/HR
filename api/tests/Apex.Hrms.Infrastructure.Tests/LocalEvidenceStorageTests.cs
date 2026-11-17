using Apex.Hrms.Infrastructure.Evidence;
using Microsoft.Extensions.Configuration;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>The evidence folder: round trips, never overwrites, refuses any key that is not 32 lower-case hex characters before touching the file system, and resolves its folder like the other App_Data settings.</summary>
public sealed class LocalEvidenceStorageTests : IDisposable
{
    private static readonly string Key = new('a', 32);

    private readonly string root = Path.Combine(Path.GetTempPath(), "apex-evidence-tests-" + Guid.NewGuid().ToString("N"));

    private LocalEvidenceStorage Storage => new(Path.Combine(root, "store"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task A_file_is_saved_read_back_byte_for_byte_and_deleted()
    {
        var content = Enumerable.Range(0, 5000).Select(value => (byte)(value % 251)).ToArray();

        await Storage.SaveAsync(Key, content, default);
        await using (var stream = await Storage.OpenReadAsync(Key, default))
        {
            Assert.NotNull(stream);
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy);
            Assert.Equal(content, copy.ToArray());
        }

        await Storage.DeleteAsync(Key, default);

        Assert.Null(await Storage.OpenReadAsync(Key, default));
        Assert.Empty(Directory.GetFiles(Path.Combine(root, "store")));
    }

    [Fact]
    public async Task The_file_name_on_disk_is_the_key_with_a_neutral_extension()
    {
        await Storage.SaveAsync(Key, [1, 2, 3], default);

        Assert.Equal([Key + ".bin"], Directory.GetFiles(Path.Combine(root, "store")).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Reading_or_deleting_a_file_that_does_not_exist_is_quiet_even_before_the_folder_exists()
    {
        Assert.Null(await Storage.OpenReadAsync(Key, default));
        await Storage.DeleteAsync(Key, default);
    }

    [Fact]
    public async Task An_existing_file_is_never_overwritten()
    {
        await Storage.SaveAsync(Key, [1, 2, 3], default);

        await Assert.ThrowsAsync<IOException>(() => Storage.SaveAsync(Key, [9, 9, 9, 9], default));

        await using var stream = await Storage.OpenReadAsync(Key, default);
        using var copy = new MemoryStream();
        await stream!.CopyToAsync(copy);
        Assert.Equal([1, 2, 3], copy.ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaag")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("..\\..\\..\\..\\windows\\win.ini")]
    [InlineData("../../../../etc/passwd")]
    [InlineData("..\\aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.")]
    [InlineData("C:\\aaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task A_key_that_is_not_32_lower_case_hex_characters_is_refused_before_any_file_access(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Storage.SaveAsync(key, [1], default));
        await Assert.ThrowsAsync<ArgumentException>(() => Storage.OpenReadAsync(key, default));
        await Assert.ThrowsAsync<ArgumentException>(() => Storage.DeleteAsync(key, default));
        Assert.False(Directory.Exists(root), "A refused key must not create the folder.");
    }

    [Fact]
    public async Task A_traversal_key_cannot_reach_a_file_outside_the_folder()
    {
        Directory.CreateDirectory(root);
        var outside = Path.Combine(root, "secret.bin");
        await File.WriteAllBytesAsync(outside, [7]);

        await Assert.ThrowsAsync<ArgumentException>(() => Storage.OpenReadAsync("..\\secret", default));
        await Assert.ThrowsAsync<ArgumentException>(() => Storage.DeleteAsync("../secret", default));

        Assert.True(File.Exists(outside));
    }

    [Fact]
    public void The_folder_defaults_to_app_data_under_the_content_root_and_a_relative_setting_starts_at_the_content_root()
    {
        var contentRoot = Path.Combine(root, "content");
        var none = new ConfigurationBuilder().Build();
        var relative = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Kpi:EvidenceFolder"] = "data/evidence" }).Build();
        var absolute = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Kpi:EvidenceFolder"] = Path.Combine(root, "elsewhere") }).Build();

        Assert.Equal(Path.Combine(contentRoot, "App_Data", "kpi-evidence"), EvidenceStorageExtensions.ResolveFolder(none, contentRoot));
        Assert.Equal(Path.Combine(contentRoot, "data", "evidence"), EvidenceStorageExtensions.ResolveFolder(relative, contentRoot));
        Assert.Equal(Path.Combine(root, "elsewhere"), EvidenceStorageExtensions.ResolveFolder(absolute, contentRoot));
    }
}
