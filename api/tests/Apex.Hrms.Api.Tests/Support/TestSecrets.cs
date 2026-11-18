using System.Security.Cryptography;

namespace Apex.Hrms.Api.Tests;

/// <summary>Throwaway values for settings that must be secret outside Development. Random per call; never a real secret.</summary>
internal static class TestSecrets
{
    /// <summary>A random Grievance:PseudonymKey for a Production test host (48 random bytes in Base64).</summary>
    public static string NewPseudonymKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}
