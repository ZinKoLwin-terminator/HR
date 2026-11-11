using System.Globalization;
using System.Text.RegularExpressions;

namespace Apex.Hrms.Application.Excel;

/// <summary>The result of reading one cell as the type of its column.</summary>
public sealed record CoercedCell(object? Value, string Text, string? Error, string? Suggestion = null);

/// <summary>The header check of an uploaded sheet against the module's columns.</summary>
public sealed record HeaderMatch(
    IReadOnlyDictionary<string, string?> Mapping,
    IReadOnlyList<string> UnknownHeaders,
    IReadOnlyList<string> AmbiguousHeaders,
    IReadOnlyList<string> MissingRequired);

/// <summary>
/// Header matching and cell coercion. Ported from <c>excel-coercion.ts</c> of the frontend mock: the same rules and the
/// same messages. These are only the shape rules of a column; business rules are the rules of the normal API, applied
/// when the row is applied.
/// </summary>
public static partial class ExcelCoercion
{
    private const string MaskedValue = "••••";
    private static readonly string[] Months = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];
    private static readonly DateTime ExcelEpoch = new(1899, 12, 30, 0, 0, 0, DateTimeKind.Utc);

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\+?[0-9][0-9\s-]{6,18}[0-9]$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^(\d{4})-(\d{2})-(\d{2})$", RegexOptions.ECMAScript)]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"^(\d{1,2})[ -]([A-Za-z]{3})[a-z]*[ -](\d{4})$", RegexOptions.ECMAScript)]
    private static partial Regex LongDate();

    [GeneratedRegex(@"^(\d{1,2}):(\d{2})(?::\d{2})?$", RegexOptions.ECMAScript)]
    private static partial Regex TimePattern();

    [GeneratedRegex(@"^[+-]?\d+(\.\d+)?$", RegexOptions.ECMAScript)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"[^\p{L}\p{N}]")]
    private static partial Regex NotLetterOrDigit();

    /// <summary>Cells starting with these characters can be run as formulas by spreadsheet software.</summary>
    [GeneratedRegex(@"^[=+\-@\t\r]", RegexOptions.ECMAScript)]
    private static partial Regex FormulaStart();

    [GeneratedRegex(@"^[+-]?\d[\d\s().,-]*$", RegexOptions.ECMAScript)]
    private static partial Regex PlainNumber();

    /// <summary>Headers are compared ignoring case, spaces, punctuation and "required" asterisks.</summary>
    public static string NormalizeHeader(string value) =>
        NotLetterOrDigit().Replace(value.ToLowerInvariant(), string.Empty);

    public static HeaderMatch MatchHeaders(IReadOnlyList<string> headers, IReadOnlyList<ExcelColumn> columns)
    {
        var index = new Dictionary<string, ExcelColumn>();
        foreach (var column in columns)
        {
            foreach (var name in new[] { column.Header, column.Key }.Concat(column.Aliases))
            {
                index[NormalizeHeader(name)] = column;
            }
        }

        var mapping = new Dictionary<string, string?>();
        var headersByColumn = new Dictionary<string, List<string>>();
        foreach (var header in headers)
        {
            var column = index.GetValueOrDefault(NormalizeHeader(header));
            mapping[header] = column?.Key;
            if (column is not null)
            {
                if (!headersByColumn.TryGetValue(column.Key, out var list))
                {
                    headersByColumn[column.Key] = list = [];
                }

                list.Add(header);
            }
        }

        return new HeaderMatch(
            mapping,
            [.. headers.Where(h => mapping[h] is null)],
            [.. headersByColumn.Values.Where(list => list.Count > 1).SelectMany(list => list)],
            [.. columns.Where(c => c.Required && !headersByColumn.ContainsKey(c.Key)).Select(c => c.Key)]);
    }

    /// <summary>Problems with a user-edited mapping (empty when it can be used).</summary>
    public static IReadOnlyList<string> MappingProblems(IReadOnlyDictionary<string, string?> mapping, IReadOnlyList<ExcelColumn> columns)
    {
        var problems = new List<string>();
        var keys = columns.Select(c => c.Key).ToHashSet();
        var used = new Dictionary<string, string>();
        foreach (var (header, key) in mapping)
        {
            if (key is null)
            {
                continue;
            }

            if (!keys.Contains(key))
            {
                problems.Add($"\"{header}\" is mapped to an unknown field.");
            }
            else if (used.TryGetValue(key, out var first))
            {
                var label = columns.First(c => c.Key == key).Header;
                problems.Add($"\"{first}\" and \"{header}\" are both mapped to {label}.");
            }
            else
            {
                used[key] = header;
            }
        }

        problems.AddRange(columns
            .Where(c => c.Required && !used.ContainsKey(c.Key))
            .Select(c => $"The required column \"{c.Header}\" is not mapped."));
        return problems;
    }

    /// <summary>The cell as text for messages and previews (dates as yyyy-MM-dd).</summary>
    public static string CellText(RawCell cell) => cell.Value switch
    {
        null => string.Empty,
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeSpan time => time.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
        double number => number.ToString("0.############", CultureInfo.InvariantCulture),
        bool flag => flag ? "TRUE" : "FALSE",
        _ => Convert.ToString(cell.Value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
    };

    /// <summary>Converts one cell to the column's type and checks the column rules (no business rules).</summary>
    public static CoercedCell Coerce(ExcelColumn column, RawCell cell)
    {
        var text = CellText(cell);
        if (cell.Formula)
        {
            return Fail(text, "Formulas are not allowed. Replace the formula with its value.", "Copy the cell and paste it as values only.");
        }

        if (text.Length == 0)
        {
            return column.Required ? Fail(text, $"{column.Header} is required.") : new CoercedCell(null, text, null);
        }

        return column.Type switch
        {
            ExcelColumnType.Text or ExcelColumnType.Email or ExcelColumnType.Phone => CoerceText(column, text),
            ExcelColumnType.Integer or ExcelColumnType.Decimal => CoerceNumber(column, cell, text),
            ExcelColumnType.Date => ToIsoDate(cell) is { } iso
                ? new CoercedCell(iso, text, null)
                : Fail(text, $"{column.Header} is not a valid date.", "Use the format yyyy-MM-dd, for example 2026-03-31."),
            ExcelColumnType.Time => ToTime(cell) is { } time
                ? new CoercedCell(time, text, null)
                : Fail(text, $"{column.Header} is not a valid time.", "Use the 24-hour format HH:mm, for example 08:30."),
            ExcelColumnType.Boolean => ToBoolean(cell) is { } flag
                ? new CoercedCell(flag, text, null)
                : Fail(text, $"{column.Header} must be Yes or No."),
            ExcelColumnType.Enum => CoerceEnum(column, text),
            _ => Fail(text, $"{column.Header} has an unsupported type."),
        };
    }

    /// <summary>The allowed value closest to the text, when it is close enough to be a likely typo.</summary>
    public static string? ClosestAllowed(string text, IReadOnlyList<string> allowed)
    {
        var needle = text.Trim().ToLowerInvariant();
        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var value in allowed)
        {
            var distance = Levenshtein(needle, value.ToLowerInvariant());
            if (distance < bestDistance)
            {
                best = value;
                bestDistance = distance;
            }
        }

        return best is not null && bestDistance <= Math.Max(1, best.Length / 4) ? best : null;
    }

    /// <summary>Key columns of a row as one comparable text (case-insensitive); null when a key part is blank.</summary>
    public static string? MatchKeyOf(ParsedRow row, IReadOnlyList<string> keys)
    {
        var parts = new List<string>();
        foreach (var key in keys)
        {
            var text = row.Text(key).Trim();
            if (text.Length == 0)
            {
                return null;
            }

            parts.Add(text.ToLowerInvariant());
        }

        return string.Join('\u0001', parts);
    }

    /// <summary>
    /// Makes text safe to write into a spreadsheet: values that could be run as a formula are prefixed with an apostrophe
    /// so they stay plain text. Ordinary numbers and phone-like text are left alone.
    /// </summary>
    public static string NeutralizeFormula(string text) =>
        FormulaStart().IsMatch(text) && !PlainNumber().IsMatch(text.Trim()) ? "'" + text : text;

    /// <summary>The text shown for a value in previews and issues: sensitive values are masked.</summary>
    public static string Shown(ExcelColumn column, string text) =>
        column.Sensitive && text.Length > 0 ? MaskedValue : text;

    private static CoercedCell Fail(string text, string error, string? suggestion = null) =>
        new(null, text, error, suggestion);

    private static CoercedCell CoerceText(ExcelColumn column, string text)
    {
        var value = column.Uppercase ? text.ToUpperInvariant() : text;
        if (column.MaxLength is { } max && text.Length > max)
        {
            return Fail(
                text,
                $"{column.Header} must be at most {max} characters (found {text.Length}).",
                $"Shorten it to {max} characters.");
        }

        if (column.MinLength is { } min && text.Length < min)
        {
            return Fail(text, $"{column.Header} must be at least {min} characters.");
        }

        if (column.Pattern is { } pattern && !Regex.IsMatch(value, pattern, RegexOptions.ECMAScript, TimeSpan.FromSeconds(1)))
        {
            return Fail(text, column.PatternMessage ?? $"{column.Header} has an invalid format.");
        }

        if (column.Type == ExcelColumnType.Email && !EmailPattern().IsMatch(text))
        {
            return Fail(text, $"{column.Header} is not a valid email address.", "Use the form name@example.com.");
        }

        if (column.Type == ExcelColumnType.Phone && !PhonePattern().IsMatch(text))
        {
            return Fail(text, $"{column.Header} is not a valid phone number.", "Use digits, spaces or dashes with an optional leading +.");
        }

        return new CoercedCell(value, text, null);
    }

    private static CoercedCell CoerceNumber(ExcelColumn column, RawCell cell, string text)
    {
        if (ToNumber(cell) is not { } number)
        {
            return Fail(text, $"{column.Header} must be a number.");
        }

        if (column.Type == ExcelColumnType.Integer && number != decimal.Truncate(number))
        {
            return Fail(text, $"{column.Header} must be a whole number.");
        }

        if (column.Type == ExcelColumnType.Decimal && column.Decimals is { } decimals
            && decimal.Round(number, decimals) != number)
        {
            return Fail(
                text,
                $"{column.Header} allows at most {decimals} decimal places.",
                $"Round it to {decimals} decimal places.");
        }

        if (column.Min is { } min && number < min)
        {
            return Fail(text, $"{column.Header} must be at least {min.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (column.Max is { } max && number > max)
        {
            return Fail(text, $"{column.Header} must be at most {max.ToString(CultureInfo.InvariantCulture)}.");
        }

        return new CoercedCell(number, text, null);
    }

    private static CoercedCell CoerceEnum(ExcelColumn column, string text)
    {
        var match = column.Allowed.FirstOrDefault(a => string.Equals(a, text, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return new CoercedCell(match, text, null);
        }

        var closest = ClosestAllowed(text, column.Allowed);
        var allowed = string.Join(", ", column.Allowed);
        return Fail(
            text,
            $"{column.Header} \"{text}\" is not an allowed value.",
            closest is not null ? $"Did you mean \"{closest}\"? Allowed: {allowed}." : $"Allowed values: {allowed}.");
    }

    private static string? IsoFromParts(int year, int month, int day) =>
        year is >= 1 and <= 9999 && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? $"{year:0000}-{month:00}-{day:00}"
            : null;

    private static string? ToIsoDate(RawCell cell)
    {
        switch (cell.Value)
        {
            case DateTime date:
                return IsoFromParts(date.Year, date.Month, date.Day);
            case double number:
                if (!double.IsFinite(number) || number < 1 || number > 2958465)
                {
                    return null;
                }

                var fromSerial = ExcelEpoch.AddDays(Math.Floor(number));
                return IsoFromParts(fromSerial.Year, fromSerial.Month, fromSerial.Day);
            case string raw:
                var text = raw.Trim();
                var iso = IsoDate().Match(text);
                if (iso.Success)
                {
                    return IsoFromParts(int.Parse(iso.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(iso.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(iso.Groups[3].Value, CultureInfo.InvariantCulture));
                }

                var long_ = LongDate().Match(text);
                if (long_.Success)
                {
                    var month = Array.IndexOf(Months, long_.Groups[2].Value.ToLowerInvariant()) + 1;
                    return month > 0
                        ? IsoFromParts(int.Parse(long_.Groups[3].Value, CultureInfo.InvariantCulture), month, int.Parse(long_.Groups[1].Value, CultureInfo.InvariantCulture))
                        : null;
                }

                return null;
            default:
                return null;
        }
    }

    private static string? ToTime(RawCell cell)
    {
        switch (cell.Value)
        {
            case DateTime date:
                return $"{date.Hour:00}:{date.Minute:00}";
            case TimeSpan span:
                return span >= TimeSpan.Zero && span < TimeSpan.FromDays(1) ? $"{span.Hours:00}:{span.Minutes:00}" : null;
            case double number:
                if (number is < 0 or >= 1)
                {
                    return null;
                }

                var minutes = (int)Math.Round(number * 24 * 60);
                return $"{minutes / 60 % 24:00}:{minutes % 60:00}";
            case string raw:
                var match = TimePattern().Match(raw.Trim());
                return match.Success
                    && int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) < 24
                    && int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) < 60
                    ? $"{int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture):00}:{match.Groups[2].Value}"
                    : null;
            default:
                return null;
        }
    }

    private static decimal? ToNumber(RawCell cell)
    {
        switch (cell.Value)
        {
            case double number:
                return double.IsFinite(number) && Math.Abs(number) < 7.9e27 ? (decimal)number : null;
            case string raw when NumberPattern().IsMatch(raw.Trim()):
                return decimal.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
            default:
                return null;
        }
    }

    private static bool? ToBoolean(RawCell cell)
    {
        if (cell.Value is bool flag)
        {
            return flag;
        }

        var text = cell.Value switch
        {
            double number => number.ToString(CultureInfo.InvariantCulture),
            string raw => raw.Trim().ToLowerInvariant(),
            _ => string.Empty,
        };
        return text switch
        {
            "yes" or "y" or "true" or "1" => true,
            "no" or "n" or "false" or "0" => false,
            _ => null,
        };
    }

    private static int Levenshtein(string a, string b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var previous = row[0];
            row[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var temp = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), previous + (a[i - 1] == b[j - 1] ? 0 : 1));
                previous = temp;
            }
        }

        return row[b.Length];
    }
}
