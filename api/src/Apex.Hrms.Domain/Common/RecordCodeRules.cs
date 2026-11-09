namespace Apex.Hrms.Domain.Common;

/// <summary>Upper-case letters, digits and dashes, starting with a letter or digit (as the CK_*_Code constraints).</summary>
public static class RecordCodeRules
{
    public const string InvalidMessage = "Use upper-case letters, numbers and dashes only.";

    public static bool IsValid(string? code) =>
        !string.IsNullOrEmpty(code)
        && IsUpperAlphanumeric(code[0])
        && code.All(character => IsUpperAlphanumeric(character) || character == '-');

    private static bool IsUpperAlphanumeric(char character) =>
        character is (>= 'A' and <= 'Z') or (>= '0' and <= '9');
}
