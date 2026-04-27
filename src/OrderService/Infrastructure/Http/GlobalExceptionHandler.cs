using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace OrderService.Infrastructure.Http;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken ct)
    {
        logger.LogError(exception, "Unhandled exception on {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        var status = exception switch
        {
            ArgumentException       => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status422UnprocessableEntity,
            _                       => StatusCodes.Status500InternalServerError
        };

        httpContext.Response.StatusCode = status;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = GetTitle(status),
            Detail = exception.Message
        }, ct);

        return true;
    }

    private static string GetTitle(int status) => status switch
    {
        400 => "Bad request",
        422 => "Unprocessable request",
        _   => "Internal server error"
    };
}
