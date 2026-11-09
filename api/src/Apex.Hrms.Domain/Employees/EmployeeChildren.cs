using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Employees;

/// <summary>Salary bank account, one per employee (hr.EmployeeBankAccount). Kept apart so access can be granted separately.</summary>
public sealed class EmployeeBankAccount : AuditedEntity
{
    private EmployeeBankAccount()
    {
    }

    public int EmployeeId { get; private set; }

    public string BankName { get; private set; } = string.Empty;

    public string? BranchName { get; private set; }

    public string AccountName { get; private set; } = string.Empty;

    /// <summary>Digits only. Never log it; show it in full only to callers with the bank permission.</summary>
    public string AccountNumber { get; private set; } = string.Empty;

    /// <summary>The account number with all but the last four digits hidden, for responses that are not bank-specific.</summary>
    public string MaskedAccountNumber => Mask(AccountNumber);

    public static string Mask(string accountNumber) =>
        accountNumber.Length <= 4
            ? new string('*', accountNumber.Length)
            : new string('*', accountNumber.Length - 4) + accountNumber[^4..];

    internal static Result<EmployeeBankAccount> Create(BankDetails details)
    {
        var account = new EmployeeBankAccount();
        var result = account.Apply(details);
        return result.IsFailure ? Result.Failure<EmployeeBankAccount>(result.Error) : Result.Success(account);
    }

    internal Result Apply(BankDetails details)
    {
        var valid = details.Validate();
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        BankName = normalized.BankName;
        BranchName = normalized.BranchName;
        AccountName = normalized.AccountName;
        AccountNumber = normalized.AccountNumber;
        return Result.Success();
    }
}

/// <summary>One line of the HR event log on the employee profile. Append-only: the server records it, nobody edits it.</summary>
public sealed class EmploymentHistoryEntry : AuditedEntity
{
    public const int TitleMaxLength = 200;
    public const int DetailsMaxLength = 1000;
    public const int RecordedByMaxLength = 130;

    private EmploymentHistoryEntry()
    {
    }

    public int EmployeeId { get; private set; }

    public DateOnly Date { get; private set; }

    public HistoryEventType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Details { get; private set; } = string.Empty;

    /// <summary>Display name of the user at the time of recording.</summary>
    public string RecordedBy { get; private set; } = string.Empty;

    internal static EmploymentHistoryEntry Record(HistoryEventType type, string title, string details, ChangeContext context) =>
        new()
        {
            Date = context.Today,
            Type = type,
            Title = Truncate(title, TitleMaxLength),
            Details = Truncate(details, DetailsMaxLength),
            RecordedBy = Truncate(context.RecordedBy, RecordedByMaxLength),
        };

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength];
}

/// <summary>The editable fields of a document. <see cref="UploadedOn"/> defaults to today when not given.</summary>
public sealed record DocumentDetails(DocumentType Type, string Title, string FileName, DateOnly? UploadedOn, DateOnly? ExpiryDate)
{
    public DocumentDetails Normalized() => this with
    {
        Title = TextNormalization.Clean(Title),
        FileName = TextNormalization.Clean(FileName),
    };
}

/// <summary>Document metadata (hr.EmployeeDocument). File contents belong in file storage, not here.</summary>
public sealed class EmployeeDocument : AuditedEntity
{
    private EmployeeDocument()
    {
    }

    public int EmployeeId { get; private set; }

    public DocumentType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public DateOnly UploadedOn { get; private set; }

    public DateOnly? ExpiryDate { get; private set; }

    public static Result<EmployeeDocument> Create(int employeeId, DocumentDetails details, DateOnly today)
    {
        var document = new EmployeeDocument { EmployeeId = employeeId };
        var result = document.Update(details, today);
        return result.IsFailure ? Result.Failure<EmployeeDocument>(result.Error) : Result.Success(document);
    }

    public static Result Validate(DocumentDetails details)
    {
        var normalized = details.Normalized();
        return new Violations()
            .Require("title", "Title", normalized.Title)
            .Require("fileName", "File name", normalized.FileName)
            .ToResult();
    }

    /// <summary>Applies the details, or changes nothing when a rule is violated.</summary>
    public Result Update(DocumentDetails details, DateOnly today)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        Type = normalized.Type;
        Title = normalized.Title;
        FileName = normalized.FileName;
        UploadedOn = normalized.UploadedOn ?? (UploadedOn == default ? today : UploadedOn);
        ExpiryDate = normalized.ExpiryDate;
        return Result.Success();
    }
}
