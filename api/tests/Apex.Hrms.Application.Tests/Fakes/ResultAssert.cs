using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Fakes;

internal static class ResultAssert
{
    public static AppError Failed(Result result, ErrorType type, string? message = null)
    {
        Assert.True(result.IsFailure, "Expected a failure but the operation succeeded.");
        Assert.Equal(type, result.Error.Type);
        if (message is not null)
        {
            Assert.Equal(message, result.Error.Message);
        }

        return result.Error;
    }

    public static AppError FailedOnField(Result result, string field, string? message = null)
    {
        var error = Failed(result, ErrorType.Validation, message);
        Assert.NotNull(error.FieldErrors);
        Assert.Contains(field, error.FieldErrors.Keys);
        return error;
    }

    public static T Succeeded<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        return result.Value;
    }
}
