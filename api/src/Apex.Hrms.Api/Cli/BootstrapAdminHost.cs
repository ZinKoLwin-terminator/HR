using System.Text;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Infrastructure;
using Apex.Hrms.Infrastructure.Evidence;
using Apex.Hrms.Infrastructure.Security;
using Microsoft.Data.SqlClient;

namespace Apex.Hrms.Api.Cli;

/// <summary>
/// Runs <see cref="BootstrapAdminCommand"/> with the real console and database. It builds the same configuration as the
/// web host (appsettings, user secrets in Development, environment variables) but only a service provider: no web server,
/// no endpoints. The command runs as the explicit system identity, the only caller allowed to write an audited row
/// without a user id.
/// </summary>
internal static class BootstrapAdminHost
{
    public static async Task<int> RunAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddBootstrapServices(builder.Configuration, builder.Environment.ContentRootPath);

        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<AdministratorBootstrap>();
        var connection = new SqlConnectionStringBuilder(builder.Configuration.GetConnectionString(Apex.Hrms.Infrastructure.DependencyInjection.ConnectionStringName));

        // Only the server and database name are shown: never credentials.
        var target = $"database '{connection.InitialCatalog}' on '{connection.DataSource}' (environment {builder.Environment.EnvironmentName})";
        var command = new BootstrapAdminCommand(bootstrap, new ConsoleTerminal(), target);
        return await command.ExecuteAsync(args, CancellationToken.None);
    }

    /// <summary>
    /// The services of the command. Application and Infrastructure register handlers that depend on the file stores and
    /// the grievance pseudonyms, and the Development host validates every registration when it is built, so those ports
    /// are registered here exactly as the web host does (<c>Program.cs</c>), even though the command never uses them.
    /// </summary>
    internal static IServiceCollection AddBootstrapServices(this IServiceCollection services, IConfiguration configuration, string contentRootPath) =>
        services
            .AddApplication()
            .AddInfrastructure(configuration)
            .AddEvidenceStorage(configuration, contentRootPath)
            .AddGrievanceFileStorage(configuration, contentRootPath)
            .AddGrievancePseudonyms(configuration)
            .UseSystemIdentity();

    private sealed class ConsoleTerminal : ITerminal
    {
        public bool IsInteractive => !Console.IsInputRedirected;

        public void WriteLine(string text) => Console.WriteLine(text);

        public string? ReadLine(string prompt)
        {
            Console.Write(prompt);
            return Console.ReadLine();
        }

        public string? ReadSecret(string prompt)
        {
            Console.Write(prompt);
            var secret = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return secret.ToString();
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (secret.Length > 0)
                    {
                        secret.Length--;
                    }
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    secret.Append(key.KeyChar);
                }
            }
        }
    }
}
