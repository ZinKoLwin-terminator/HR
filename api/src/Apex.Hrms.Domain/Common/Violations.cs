namespace Apex.Hrms.Domain.Common;

/// <summary>Collects field-level rule violations so every problem is reported at once.</summary>
public sealed class Violations
{
    private const string MultipleMessage = "One or more validation errors occurred.";

    private readonly Dictionary<string, List<string>> byField = [];
    private string? singleSummary;
    private int count;

    public bool Any => count > 0;

    /// <param name="field">camelCase field name.</param>
    /// <param name="message">Message shown next to the field.</param>
    /// <param name="summary">Overall message used when this is the only violation.</param>
    public Violations Add(string field, string message, string? summary = null)
    {
        if (!byField.TryGetValue(field, out var messages))
        {
            messages = [];
            byField[field] = messages;
        }

        messages.Add(message);
        singleSummary = summary ?? message;
        count++;
        return this;
    }

    public Violations AddIf(bool condition, string field, string message, string? summary = null) =>
        condition ? Add(field, message, summary) : this;

    /// <summary>Adds a violation when a required text value is missing or whitespace.</summary>
    public Violations Require(string field, string label, string? value) =>
        AddIf(string.IsNullOrWhiteSpace(value), field, $"{label} is required.");

    public Result ToResult() => Any
        ? Result.Failure(ToError())
        : Result.Success();

    public AppError ToError() => AppError.Validation(
        count == 1 ? singleSummary! : MultipleMessage,
        byField.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
}
