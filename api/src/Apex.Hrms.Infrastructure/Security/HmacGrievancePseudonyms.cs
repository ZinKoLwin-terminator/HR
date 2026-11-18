using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Apex.Hrms.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Infrastructure.Security;

/// <summary>
/// HMACSHA256 over <c>apex-hrms:grievance-submitter:&lt;employee id&gt;</c> (invariant digits, UTF-8) with the configured key (the
/// Base64-decoded bytes of <c>Grievance:PseudonymKey</c>). The prefix keeps these values apart from any other use of the same key.
/// Neither the key nor a pseudonym is ever logged.
/// </summary>
internal sealed class HmacGrievancePseudonyms : IGrievancePseudonyms
{
    private const string Purpose = "apex-hrms:grievance-submitter:";

    private readonly byte[] key;

    public HmacGrievancePseudonyms(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length < GrievancePseudonymSettings.MinimumKeyBytes)
        {
            throw new ArgumentException($"A pseudonym key is at least {GrievancePseudonymSettings.MinimumKeyBytes} bytes.", nameof(key));
        }

        this.key = [.. key];
    }

    public byte[] Compute(int employeeId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(employeeId);
        var message = Encoding.UTF8.GetBytes(Purpose + employeeId.ToString(CultureInfo.InvariantCulture));
        return HMACSHA256.HashData(key, message);
    }
}

/// <summary>
/// Reads <c>Grievance:PseudonymKey</c> and refuses to start without a usable one. The key is Base64 and must decode to at least
/// <see cref="MinimumKeyBytes"/> bytes (for example 48 random bytes: <c>[Convert]::ToBase64String((1..48 | % { Get-Random -Max 256 }))</c>
/// or <c>openssl rand -base64 48</c>); set it with user-secrets or the environment (<c>Grievance__PseudonymKey</c>), never in an
/// appsettings file, source control or a log. The labelled development value of <c>appsettings.Development.json</c> is refused in EVERY
/// environment unless the connection string points at LocalDB or this machine, so it can never protect real data. The key must never
/// change once anonymous cases exist: a new key cuts every anonymous case off from its submitter for good.
/// </summary>
public static class GrievancePseudonymSettings
{
    public const string KeyName = "Grievance:PseudonymKey";

    /// <summary>
    /// The value in appsettings.Development.json: Base64 of "DEVELOPMENT-ONLY grievance pseudonym key. Not a secret. LocalDB only."
    /// Accepted only while the connection string targets LocalDB or localhost.
    /// </summary>
    public const string DevelopmentPlaceholder = "REVWRUxPUE1FTlQtT05MWSBncmlldmFuY2UgcHNldWRvbnltIGtleS4gTm90IGEgc2VjcmV0LiBMb2NhbERCIG9ubHku";

    public const int MinimumKeyBytes = 32;

    private const string ConnectionStringName = "ConnectionStrings:Hrms";

    /// <summary>The decoded key.</summary>
    /// <exception cref="InvalidOperationException">
    /// The key is missing, not Base64, shorter than 32 bytes, or the development value while the database is not local. The message never
    /// contains the key.
    /// </exception>
    public static byte[] Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var text = configuration[KeyName]?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            throw new InvalidOperationException(
                $"{KeyName} is not configured. Set a Base64 key of at least {MinimumKeyBytes} bytes with user-secrets or the environment variable Grievance__PseudonymKey.");
        }

        var buffer = new byte[(text.Length * 3 / 4) + 3];
        if (!Convert.TryFromBase64String(text, buffer, out var written))
        {
            throw new InvalidOperationException($"{KeyName} must be Base64 (for example 48 random bytes in Base64).");
        }

        if (written < MinimumKeyBytes)
        {
            throw new InvalidOperationException($"{KeyName} must decode to at least {MinimumKeyBytes} bytes.");
        }

        if (string.Equals(text, DevelopmentPlaceholder, StringComparison.Ordinal) && !TargetsLocalDatabase(configuration[ConnectionStringName]))
        {
            throw new InvalidOperationException(
                $"{KeyName} is the development value, which is accepted only with a LocalDB or localhost database. Set a real secret with user-secrets or the environment.");
        }

        return buffer[..written];
    }

    /// <summary>True when the connection string's server is LocalDB or this machine.</summary>
    public static bool TargetsLocalDatabase(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        string server;
        try
        {
            server = new SqlConnectionStringBuilder(connectionString).DataSource.Trim();
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (server.StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var host = server;
        foreach (var prefix in new[] { "tcp:", "np:", "lpc:" })
        {
            if (host.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                host = host[prefix.Length..];
            }
        }

        host = host.Split('\\', ',')[0].Trim();
        return host is "." or "(local)" or "127.0.0.1" or "::1"
            || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("(local)", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Registers <see cref="IGrievancePseudonyms"/>; fails at startup when <see cref="Resolve"/> refuses the configuration.</summary>
    public static IServiceCollection AddGrievancePseudonyms(this IServiceCollection services, IConfiguration configuration)
    {
        var key = Resolve(configuration);
        services.Replace(ServiceDescriptor.Singleton<IGrievancePseudonyms>(new HmacGrievancePseudonyms(key)));
        return services;
    }
}
