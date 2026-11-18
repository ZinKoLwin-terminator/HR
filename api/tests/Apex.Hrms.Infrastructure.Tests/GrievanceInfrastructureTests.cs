using System.Security.Cryptography;
using System.Text;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Infrastructure.Evidence;
using Apex.Hrms.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>
/// The grievance file folder (its own folder with the evidence-file rules, never the KPI folder) and the keyed pseudonym of an
/// anonymous submitter (HMACSHA256, stable, keyed, and refused at startup without a real key outside Development).
/// </summary>
public sealed class GrievanceInfrastructureTests : IDisposable
{
    private static readonly string Key = new('b', 32);

    private static readonly string TestKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    private readonly string root = Path.Combine(Path.GetTempPath(), "apex-grievance-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();

    // ---- File storage ----

    [Fact]
    public async Task A_grievance_file_round_trips_in_its_own_folder_and_is_never_overwritten()
    {
        var storage = new LocalGrievanceFileStorage(Path.Combine(root, "grievance"));

        await storage.SaveAsync(Key, [1, 2, 3], default);
        await Assert.ThrowsAnyAsync<IOException>(() => storage.SaveAsync(Key, [4], default));
        await using (var stream = await storage.OpenReadAsync(Key, default))
        {
            Assert.NotNull(stream);
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy);
            Assert.Equal([1, 2, 3], copy.ToArray());
        }

        Assert.Equal([Key + ".bin"], Directory.GetFiles(Path.Combine(root, "grievance")).Select(Path.GetFileName));
        await storage.DeleteAsync(Key, default);
        Assert.Null(await storage.OpenReadAsync(Key, default));
    }

    [Theory]
    [InlineData("../../escape")]
    [InlineData("BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB")]
    [InlineData("report.pdf")]
    public async Task A_key_that_is_not_32_lower_case_hex_characters_is_refused(string key)
    {
        var storage = new LocalGrievanceFileStorage(Path.Combine(root, "grievance"));

        await Assert.ThrowsAsync<ArgumentException>(() => storage.SaveAsync(key, [1], default));
        Assert.False(Directory.Exists(Path.Combine(root, "grievance")));
    }

    [Fact]
    public void The_folder_defaults_to_app_data_under_the_content_root_and_a_relative_setting_starts_there()
    {
        Assert.Equal(
            Path.GetFullPath(Path.Combine(root, "App_Data", "grievance-files")),
            GrievanceFileStorageExtensions.ResolveFolder(Configuration(), root));
        Assert.Equal(
            Path.GetFullPath(Path.Combine(root, "files")),
            GrievanceFileStorageExtensions.ResolveFolder(Configuration((GrievanceFileStorageExtensions.FolderKey, "files")), root));
    }

    [Fact]
    public void The_grievance_storage_is_registered_apart_from_the_kpi_evidence_storage()
    {
        var configuration = Configuration(
            (EvidenceStorageExtensions.FolderKey, Path.Combine(root, "kpi")),
            (GrievanceFileStorageExtensions.FolderKey, Path.Combine(root, "grievance")));
        using var provider = new ServiceCollection()
            .AddEvidenceStorage(configuration, root)
            .AddGrievanceFileStorage(configuration, root)
            .BuildServiceProvider();

        var grievance = provider.GetRequiredService<IGrievanceFileStorage>();
        var evidence = provider.GetRequiredService<IEvidenceStorage>();

        Assert.IsType<LocalGrievanceFileStorage>(grievance);
        Assert.NotSame(evidence, grievance);
    }

    [Fact]
    public void The_grievance_folder_cannot_be_the_kpi_evidence_folder()
    {
        var configuration = Configuration(
            (EvidenceStorageExtensions.FolderKey, Path.Combine(root, "shared")),
            (GrievanceFileStorageExtensions.FolderKey, Path.Combine(root, "shared") + Path.DirectorySeparatorChar));

        var exception = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddGrievanceFileStorage(configuration, root));

        Assert.Contains(GrievanceFileStorageExtensions.FolderKey, exception.Message, StringComparison.Ordinal);
    }


    // ---- Pseudonyms ----

    private static byte[] KeyBytes => Convert.FromBase64String(TestKey);

    [Fact]
    public void A_pseudonym_is_the_hmac_sha256_of_the_purpose_and_the_employee_id_with_the_decoded_key()
    {
        var expected = HMACSHA256.HashData(KeyBytes, Encoding.UTF8.GetBytes("apex-hrms:grievance-submitter:42"));

        var pseudonym = new HmacGrievancePseudonyms(KeyBytes).Compute(42);

        Assert.Equal(IGrievancePseudonyms.Length, pseudonym.Length);
        Assert.Equal(expected, pseudonym);
    }

    [Fact]
    public void The_same_employee_always_gets_the_same_pseudonym_and_others_or_other_keys_do_not()
    {
        var pseudonyms = new HmacGrievancePseudonyms(KeyBytes);
        var otherKey = KeyBytes;
        otherKey[0] ^= 0xFF;

        Assert.Equal(pseudonyms.Compute(7), pseudonyms.Compute(7));
        Assert.NotEqual(pseudonyms.Compute(7), pseudonyms.Compute(8));
        Assert.NotEqual(pseudonyms.Compute(7), new HmacGrievancePseudonyms(otherKey).Compute(7));
    }

    [Fact]
    public void Each_call_returns_a_new_array()
    {
        var pseudonyms = new HmacGrievancePseudonyms(KeyBytes);
        var first = pseudonyms.Compute(7);
        first[0] ^= 0xFF;

        Assert.NotEqual(first, pseudonyms.Compute(7));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void An_employee_id_must_be_positive(int employeeId) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new HmacGrievancePseudonyms(KeyBytes).Compute(employeeId));

    [Fact]
    public void A_missing_key_fails()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => GrievancePseudonymSettings.Resolve(Configuration()));

        Assert.Contains(GrievancePseudonymSettings.KeyName, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not base64 at all!")]
    [InlineData("c2hvcnQta2V5")]
    public void A_key_that_is_not_base64_or_decodes_to_fewer_than_32_bytes_is_refused_and_never_echoed(string key)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            GrievancePseudonymSettings.Resolve(Configuration((GrievancePseudonymSettings.KeyName, key))));

        Assert.DoesNotContain(key, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"Server=(localdb)\MSSQLLocalDB;Database=HR;Integrated Security=True")]
    [InlineData("Server=localhost;Database=HR;Integrated Security=True")]
    [InlineData(@"Server=.\SQLEXPRESS;Database=HR;Integrated Security=True")]
    [InlineData("Server=tcp:127.0.0.1,1433;Database=HR;Integrated Security=True")]
    public void The_development_value_is_accepted_with_a_local_database(string connectionString)
    {
        var configuration = Configuration(
            (GrievancePseudonymSettings.KeyName, GrievancePseudonymSettings.DevelopmentPlaceholder),
            ("ConnectionStrings:Hrms", connectionString));

        Assert.True(GrievancePseudonymSettings.Resolve(configuration).Length >= GrievancePseudonymSettings.MinimumKeyBytes);
    }

    [Theory]
    [InlineData("Server=db.example.test;Database=HR;Integrated Security=True")]
    [InlineData("Server=tcp:10.0.0.5,1433;Database=HR;User Id=app;Password=placeholder")]
    [InlineData(null)]
    public void The_development_value_is_refused_with_any_other_database_in_every_environment(string? connectionString)
    {
        var configuration = Configuration(
            (GrievancePseudonymSettings.KeyName, GrievancePseudonymSettings.DevelopmentPlaceholder),
            ("ConnectionStrings:Hrms", connectionString));

        var exception = Assert.Throws<InvalidOperationException>(() => GrievancePseudonymSettings.Resolve(configuration));

        Assert.DoesNotContain(GrievancePseudonymSettings.DevelopmentPlaceholder, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_development_value_is_labelled_when_decoded()
    {
        Assert.StartsWith("DEVELOPMENT-ONLY", Encoding.ASCII.GetString(Convert.FromBase64String(GrievancePseudonymSettings.DevelopmentPlaceholder)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_real_key_registers_the_pseudonym_service()
    {
        using var provider = new ServiceCollection()
            .AddGrievancePseudonyms(Configuration((GrievancePseudonymSettings.KeyName, TestKey), ("ConnectionStrings:Hrms", "Server=db.example.test;Database=HR")))
            .BuildServiceProvider();

        Assert.Equal(new HmacGrievancePseudonyms(KeyBytes).Compute(3), provider.GetRequiredService<IGrievancePseudonyms>().Compute(3));
    }
}
