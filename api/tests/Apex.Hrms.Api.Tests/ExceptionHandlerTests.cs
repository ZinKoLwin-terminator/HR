using System.Text.Json;
using Apex.Hrms.Api.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Apex.Hrms.Api.Tests;

public sealed class ExceptionHandlerTests
{
    private const string Secret = "SECRET-VALUE-jane.doe@example.test";

    private sealed class CapturingProblemService : IProblemDetailsService
    {
        public List<ProblemDetailsContext> Written { get; } = [];

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Written.Add(context);
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Written.Add(context);
            return ValueTask.FromResult(true);
        }
    }

    private sealed class CapturingLogger : ILogger<ApiExceptionHandler>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // Mirrors a console logger that also prints the exception, if one were passed.
            Messages.Add(formatter(state, exception) + (exception is null ? string.Empty : exception.ToString()));
        }
    }

    private readonly CapturingProblemService problems = new();
    private readonly CapturingLogger logger = new();

    private static Exception Thrown(Exception exception)
    {
        try
        {
            throw exception;
        }
        catch (Exception caught)
        {
            return caught;
        }
    }

    [Fact]
    public async Task An_unexpected_exception_becomes_a_generic_500_and_the_log_never_contains_its_message()
    {
        var handler = new ApiExceptionHandler(problems, logger);
        var context = new DefaultHttpContext();
        var exception = Thrown(new InvalidOperationException($"Duplicate key value is ({Secret})", new KeyNotFoundException(Secret)));

        var handled = await handler.TryHandleAsync(context, exception, default);

        Assert.True(handled);
        Assert.Equal(500, context.Response.StatusCode);
        var problem = Assert.Single(problems.Written).ProblemDetails;
        Assert.Equal("Unknown", problem.Extensions["code"]?.ToString());
        Assert.Equal("An unexpected error occurred.", problem.Detail);
        var logged = Assert.Single(logger.Messages);
        Assert.DoesNotContain(Secret, logged, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException > KeyNotFoundException", logged, StringComparison.Ordinal);
        Assert.Contains(nameof(Thrown), logged, StringComparison.Ordinal); // a frame of the stack trace
    }

    [Theory]
    [InlineData(400, 400, "The request body or parameters are malformed.")]
    [InlineData(413, 413, "The request body is too large.")]
    [InlineData(415, 415, "The request content type is not supported.")]
    [InlineData(408, 408, "The request took too long to arrive.")]
    public async Task A_client_error_keeps_its_own_status_and_is_a_validation_problem_that_is_not_logged_as_an_error(
        int status,
        int expectedStatus,
        string detail)
    {
        var handler = new ApiExceptionHandler(problems, logger);
        var context = new DefaultHttpContext();

        await handler.TryHandleAsync(context, new BadHttpRequestException(Secret, status), default);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        var problem = Assert.Single(problems.Written).ProblemDetails;
        Assert.Equal(expectedStatus, problem.Status);
        Assert.Equal(detail, problem.Detail);
        Assert.Equal("Validation", problem.Extensions["code"]?.ToString());
        Assert.Empty(logger.Messages);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(problem), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_request_aborted_by_the_client_writes_no_problem_and_logs_no_error()
    {
        using var aborted = new CancellationTokenSource();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        await aborted.CancelAsync();
        var handler = new ApiExceptionHandler(problems, logger);

        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(aborted.Token), default);

        Assert.True(handled);
        Assert.Equal(ApiExceptionHandler.ClientClosedRequest, context.Response.StatusCode);
        Assert.Empty(problems.Written);
        Assert.Empty(logger.Messages);
    }

    [Fact]
    public async Task A_cancellation_that_the_client_did_not_cause_is_an_unexpected_error()
    {
        var context = new DefaultHttpContext();
        var handler = new ApiExceptionHandler(problems, logger);

        await handler.TryHandleAsync(context, Thrown(new OperationCanceledException("server-side timeout")), default);

        Assert.Equal(500, context.Response.StatusCode);
        Assert.Single(logger.Messages);
    }
}
