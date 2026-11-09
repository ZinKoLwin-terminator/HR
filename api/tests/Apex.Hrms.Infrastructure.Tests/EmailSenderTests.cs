using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apex.Hrms.Infrastructure.Tests;

public sealed class EmailSenderTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "hrms-mail-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private sealed class CapturingLogger : ILogger<DevelopmentMailSink>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    [Fact]
    public async Task The_development_sink_writes_one_file_with_the_message_and_sends_nothing()
    {
        var sink = new DevelopmentMailSink(folder, NullLogger<DevelopmentMailSink>.Instance);

        await sink.SendAsync(new EmailMessage("tst-user@example.test", "Reset", "token-ABC"), default);

        var file = Assert.Single(Directory.GetFiles(folder, "*.eml"));
        var content = await File.ReadAllTextAsync(file);
        Assert.Contains("To: tst-user@example.test", content, StringComparison.Ordinal);
        Assert.Contains("Subject: Reset", content, StringComparison.Ordinal);
        Assert.Contains("token-ABC", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_development_sink_logs_only_that_a_file_was_written()
    {
        var logger = new CapturingLogger();
        var sink = new DevelopmentMailSink(folder, logger);

        await sink.SendAsync(new EmailMessage("tst-user@example.test", "Reset", "token-ABC"), default);

        var message = Assert.Single(logger.Messages);
        Assert.DoesNotContain("tst-user", message, StringComparison.Ordinal);
        Assert.DoesNotContain("token-ABC", message, StringComparison.Ordinal);
        Assert.DoesNotContain(folder, message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("smtp.example.test", null)]
    [InlineData(null, "hr@example.test")]
    [InlineData("", "hr@example.test")]
    public async Task The_smtp_sender_refuses_when_it_is_not_configured(string? host, string? from)
    {
        var sender = new SmtpEmailSender(new SmtpSettings(host, 587, null, null, from, null, true));

        Assert.Throws<EmailNotConfiguredException>(sender.EnsureAvailable);
        await Assert.ThrowsAsync<EmailNotConfiguredException>(() => sender.SendAsync(new EmailMessage("tst@example.test", "s", "b"), default));
    }

    [Fact]
    public void The_smtp_sender_refuses_a_user_name_without_a_password()
    {
        var sender = new SmtpEmailSender(new SmtpSettings("smtp.example.test", 587, "user", null, "hr@example.test", null, true));

        Assert.Throws<EmailNotConfiguredException>(sender.EnsureAvailable);
    }

    [Fact]
    public void The_configuration_error_names_the_settings_and_no_value()
    {
        var sender = new SmtpEmailSender(new SmtpSettings(null, 587, "secret-user", "secret-password", null, null, true));

        var exception = Assert.Throws<EmailNotConfiguredException>(sender.EnsureAvailable);

        Assert.Contains("Email:Smtp:Host", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-user", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-password", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_complete_smtp_configuration_is_available_and_the_settings_are_read_from_configuration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Smtp:Host"] = "smtp.example.test",
            ["Email:Smtp:FromAddress"] = "hr@example.test",
            ["Email:Smtp:Port"] = "2525",
        }).Build();

        var settings = SmtpSettings.From(configuration);

        Assert.True(settings.IsConfigured);
        Assert.Equal(2525, settings.Port);
        Assert.True(settings.EnableSsl);
        new SmtpEmailSender(settings).EnsureAvailable();
    }

    [Fact]
    public void Development_registers_the_file_sink_and_other_environments_the_smtp_sender()
    {
        var configuration = new ConfigurationBuilder().Build();

        var development = new ServiceCollection().AddLogging().AddEmailSender(configuration, isDevelopment: true, folder).BuildServiceProvider();
        var production = new ServiceCollection().AddLogging().AddEmailSender(configuration, isDevelopment: false, folder).BuildServiceProvider();

        Assert.IsType<DevelopmentMailSink>(development.GetRequiredService<IEmailSender>());
        Assert.IsType<SmtpEmailSender>(production.GetRequiredService<IEmailSender>());
        Assert.Throws<EmailNotConfiguredException>(production.GetRequiredService<IEmailSender>().EnsureAvailable);
    }
}
