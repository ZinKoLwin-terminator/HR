namespace Apex.Hrms.Domain.Common;

public enum RecordStatus
{
    Active,
    Inactive,
}

public static class RecordStatusText
{
    /// <summary>Parses the exact (case-sensitive) names used by the API and the database; numbers are rejected.</summary>
    public static bool TryParse(string? text, out RecordStatus status)
    {
        status = default;
        return text is not null
            && Enum.GetNames<RecordStatus>().Contains(text, StringComparer.Ordinal)
            && Enum.TryParse(text, ignoreCase: false, out status);
    }
}
