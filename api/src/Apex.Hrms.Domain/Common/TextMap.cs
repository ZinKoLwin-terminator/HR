namespace Apex.Hrms.Domain.Common;

/// <summary>
/// Converts an enum to and from the exact (case-sensitive) texts the API and the database use, for example
/// <c>OnLeave</c> and <c>"On Leave"</c>. Numbers and other spellings are rejected.
/// </summary>
public sealed class TextMap<TEnum>
    where TEnum : struct, Enum
{
    private readonly Dictionary<TEnum, string> texts;

    public TextMap(IReadOnlyDictionary<TEnum, string> texts) => this.texts = new Dictionary<TEnum, string>(texts);

    /// <summary>"A, B or C": the accepted texts for a validation message.</summary>
    public string OneOf
    {
        get
        {
            var all = texts.Values.ToList();
            return all.Count == 1 ? all[0] : $"{string.Join(", ", all[..^1])} or {all[^1]}";
        }
    }

    public string ToText(TEnum value) => texts[value];

    /// <exception cref="ArgumentException">The text is not known.</exception>
    public TEnum Parse(string? text) =>
        TryParse(text, out var value) ? value : throw new ArgumentException("Unknown value.", nameof(text));

    public bool TryParse(string? text, out TEnum value)
    {
        foreach (var (candidate, candidateText) in texts)
        {
            if (candidateText == text)
            {
                value = candidate;
                return true;
            }
        }

        value = default;
        return false;
    }
}
