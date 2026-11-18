using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// The grievance module's host settings: the pseudonym key is required (the development placeholder only in Development), the
/// grievance file storage and the pseudonyms are registered, and the four permission modules have their policies. No database.
/// </summary>
public sealed class GrievanceHostingTests
{
    private static WebApplicationFactory<Program> Production(string? pseudonymKey) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            // A database that is not on this machine: the development pseudonym value is refused with it.
            builder.UseSetting("ConnectionStrings:Hrms", "Server=db.example.test;Database=Placeholder;Integrated Security=True");
            builder.UseSetting("AllowedHosts", "api.example.test");
            builder.UseSetting("https_port", "443");
            if (pseudonymKey is not null)
            {
                builder.UseSetting(GrievancePseudonymSettings.KeyName, pseudonymKey);
            }
        });

    [Theory]
    [InlineData(null)]
    [InlineData(GrievancePseudonymSettings.DevelopmentPlaceholder)]
    public void Production_refuses_to_start_without_a_real_pseudonym_key(string? pseudonymKey)
    {
        using var factory = Production(pseudonymKey);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(GrievancePseudonymSettings.KeyName, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Production_starts_with_a_real_pseudonym_key_from_the_environment_settings()
    {
        using var factory = Production(TestSecrets.NewPseudonymKey());

        using var client = factory.CreateClient();

        Assert.Equal(IGrievancePseudonyms.Length, factory.Services.GetRequiredService<IGrievancePseudonyms>().Compute(1).Length);
    }

    [Fact]
    public void Development_uses_the_placeholder_of_its_settings_and_registers_the_grievance_services()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        Assert.Equal(GrievancePseudonymSettings.DevelopmentPlaceholder, factory.Services.GetRequiredService<IConfiguration>()[GrievancePseudonymSettings.KeyName]);
        Assert.NotNull(factory.Services.GetRequiredService<IGrievancePseudonyms>());
        Assert.NotSame(factory.Services.GetRequiredService<IEvidenceStorage>(), factory.Services.GetRequiredService<IGrievanceFileStorage>());
    }

    [Fact]
    public void The_four_grievance_modules_have_exactly_their_seeded_actions()
    {
        var grievance = PermissionModule.All
            .Where(module => module.Key.StartsWith("grievance", StringComparison.Ordinal))
            .ToDictionary(module => module.Key, module => module.Permissions.Select(permission => permission.ToString()).ToArray());

        Assert.Equal(4, grievance.Count);
        Assert.Equal(["View", "Create"], grievance["grievance"]);
        Assert.Equal(["View", "Edit"], grievance["grievance-investigation"]);
        Assert.Equal(["View", "Create", "Edit", "Delete"], grievance["grievance-management"]);
        Assert.Equal(["View", "Approve"], grievance["grievance-review"]);
    }
}
