using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Api.Cli;

/// <summary>The console as the command sees it; a test supplies a scripted one.</summary>
internal interface ITerminal
{
    /// <summary>False when input is redirected (a pipe or file): a password cannot be hidden then, so it is refused.</summary>
    bool IsInteractive { get; }

    void WriteLine(string text);

    string? ReadLine(string prompt);

    /// <summary>Reads a secret without echoing it.</summary>
    string? ReadSecret(string prompt);
}

/// <summary>
/// <c>bootstrap-admin --email &lt;address&gt; [--name &lt;display name&gt;]</c>: creates the first HR Administrator (identity user
/// and business account, no employee) in one transaction. The password is typed at a hidden prompt: it is never an
/// argument, an environment variable, a file or a setting. It refuses when an active linked administrator exists.
/// </summary>
internal sealed class BootstrapAdminCommand(AdministratorBootstrap bootstrap, ITerminal terminal, string target)
{
    public const string Name = "bootstrap-admin";
    public const string DefaultDisplayName = "System Administrator";

    public const int Succeeded = 0;
    public const int Failed = 1;
    public const int UsageError = 2;

    public static string Usage => $"Usage: {Name} --email <address> [--name <display name>]";

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken)
    {
        if (!TryParse(args, out var email, out var displayName))
        {
            terminal.WriteLine(Usage);
            return UsageError;
        }

        if (!terminal.IsInteractive)
        {
            terminal.WriteLine("This command needs an interactive terminal: the password is typed at a hidden prompt and cannot be piped.");
            return UsageError;
        }

        terminal.WriteLine($"Target: {target}");
        if (!string.Equals(terminal.ReadLine("Create the HR Administrator in this database? Type 'yes' to continue: "), "yes", StringComparison.Ordinal))
        {
            terminal.WriteLine("Cancelled. Nothing was changed.");
            return Failed;
        }

        var password = terminal.ReadSecret($"Password (at least {SecurityPolicy.PasswordMinimumLength} characters): ");
        var confirmation = terminal.ReadSecret("Repeat the password: ");
        if (string.IsNullOrEmpty(password) || !string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            terminal.WriteLine("The passwords are empty or do not match. Nothing was changed.");
            return Failed;
        }

        var result = await bootstrap.RunAsync(email, displayName, password, cancellationToken);
        if (result.IsSuccess)
        {
            terminal.WriteLine("The HR Administrator was created. Sign in with that email address and the password you just typed.");
            return Succeeded;
        }

        terminal.WriteLine($"Refused: {Describe(result.Error)}");
        return Failed;
    }

    private static string Describe(AppError error) =>
        error.FieldErrors is { Count: > 0 }
            ? $"{error.Message} {string.Join(" ", error.FieldErrors.Values.SelectMany(messages => messages))}"
            : error.Message;

    private static bool TryParse(string[] args, out string email, out string displayName)
    {
        email = string.Empty;
        displayName = DefaultDisplayName;
        var haveEmail = false;
        for (var i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
            {
                return false;
            }

            switch (args[i])
            {
                case "--email":
                    email = args[i + 1];
                    haveEmail = true;
                    break;
                case "--name":
                    displayName = args[i + 1];
                    break;
                default:
                    return false;
            }
        }

        return haveEmail;
    }
}
