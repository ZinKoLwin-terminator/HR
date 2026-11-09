namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// An audited row was added or changed while nobody was signed in (and the caller is not the system identity).
/// <see cref="EfUnitOfWork"/> turns it into an Unauthorized AppError (401); the API exception handler does the same
/// for code that saves directly. The message carries no data.
/// </summary>
public sealed class UnauthenticatedWriteException : InvalidOperationException
{
    public const string PublicMessage = "Sign in to make this change.";

    public UnauthenticatedWriteException()
        : base(PublicMessage)
    {
    }

    public UnauthenticatedWriteException(string message)
        : base(message)
    {
    }

    public UnauthenticatedWriteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
