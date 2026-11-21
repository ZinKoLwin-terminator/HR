using System.Globalization;
using System.Security.Claims;
using Apex.Hrms.Mobile.Application.Abstractions;
using Apex.Hrms.Mobile.Application.Auth;

namespace Apex.Hrms.MobileApi.Auth;

/// <summary>Reads the caller out of a validated access token. Inbound claim mapping is off, so the JWT names arrive unchanged.</summary>
internal static class MobileClaims
{
    /// <summary>The caller, or null when a claim is missing or malformed.</summary>
    public static MobileCaller? Read(ClaimsPrincipal? principal)
    {
        var subject = principal?.FindFirstValue(MobileClaimTypes.Subject);
        var deviceId = principal?.FindFirstValue(MobileClaimTypes.SessionId);
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(deviceId)
            || !int.TryParse(principal!.FindFirstValue(MobileClaimTypes.AccountId), NumberStyles.None, CultureInfo.InvariantCulture, out var accountId))
        {
            return null;
        }

        return new MobileCaller(subject, deviceId, accountId);
    }
}
