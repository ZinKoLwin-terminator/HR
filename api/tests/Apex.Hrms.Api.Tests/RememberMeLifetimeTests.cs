using Apex.Hrms.Api.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apex.Hrms.Api.Tests;

/// <summary>Security re-review L2: a "remember me" session has an absolute maximum lifetime (30 days by default, configurable).</summary>
public sealed class RememberMeLifetimeTests
{
    private static TimeSpan Maximum(string? configured)
    {
        var settings = new Dictionary<string, string?>();
        if (configured is not null)
        {
            settings[AuthFlows.RememberMeMaxDaysKey] = configured;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var flows = new AuthFlows(null!, null!, null!, null!, TimeProvider.System, configuration, new TestEnvironment(), NullLogger<AuthFlows>.Instance);
        return flows.RememberMeAbsoluteMaximum();
    }

    [Fact]
    public void The_default_is_thirty_days_and_longer_than_the_sliding_window()
    {
        Assert.Equal(TimeSpan.FromDays(30), Maximum(null));
        Assert.True(Maximum(null) > AuthFlows.RememberWindow);
    }

    [Theory]
    [InlineData("14", 14)]
    [InlineData("1", 1)]
    [InlineData("365", 365)]
    public void The_configured_number_of_days_is_used(string configured, int days) =>
        Assert.Equal(TimeSpan.FromDays(days), Maximum(configured));

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("366")]
    [InlineData("abc")]
    [InlineData("")]
    public void An_unusable_value_falls_back_to_thirty_days(string configured) =>
        Assert.Equal(TimeSpan.FromDays(30), Maximum(configured));

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";

        public string ApplicationName { get; set; } = "Test";

        public string ContentRootPath { get; set; } = string.Empty;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
