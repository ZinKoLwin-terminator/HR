namespace Apex.Hrms.Domain.Common;

internal static class TextNormalization
{
    public static string Clean(string value) => value.Trim();

    /// <summary>Optional text columns store NULL rather than an empty string.</summary>
    public static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
