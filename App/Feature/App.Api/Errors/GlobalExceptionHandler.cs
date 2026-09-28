using App.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Errors;

// Translates exceptions into RFC 9457 problem details. Expected domain failures get a specific status
// code and their own message; anything else is logged and returned as an opaque 500.
public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    // Non-standard but widely used (nginx) status for "client closed the connection".
    private const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var problem = exception switch
        {
            ValidationException validation => new ValidationProblemDetails(
                new Dictionary<string, string[]> { [validation.Field] = [validation.Message] })
            {
                Status = StatusCodes.Status400BadRequest,
            },
            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found",
                Detail = notFound.Message,
            },
            UpstreamServiceException upstream => CreateUpstreamProblem(httpContext, upstream),
            _ => null,
        };

        if (problem is null)
        {
            LogUnhandledException(exception, httpContext.Request.Method, httpContext.Request.Path);

            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred",
            };
        }
        else if (exception is UpstreamServiceException)
        {
            LogUpstreamFailure(exception, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    // A rate limit with a known reset time is temporary (503 + Retry-After); anything else is a bad
    // upstream response (502).
    private static ProblemDetails CreateUpstreamProblem(HttpContext httpContext, UpstreamServiceException exception)
    {
        if (exception.RetryAfter is { } retryAfter)
        {
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

            return new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = $"{exception.Service} is temporarily unavailable",
                Detail = exception.Message,
            };
        }

        return new ProblemDetails
        {
            Status = StatusCodes.Status502BadGateway,
            Title = $"{exception.Service} request failed",
            Detail = exception.Message,
        };
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception processing {Method} {Path}")]
    private partial void LogUnhandledException(Exception exception, string method, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upstream failure processing {Path}")]
    private partial void LogUpstreamFailure(Exception exception, string path);
}
