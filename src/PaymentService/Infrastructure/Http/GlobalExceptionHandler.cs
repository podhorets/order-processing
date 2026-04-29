using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PaymentService.Infrastructure.Http;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        logger.LogError(ex, "Unhandled exception on {Method} {Path}",
            ctx.Request.Method, ctx.Request.Path);

        var status = ex switch
        {
            ArgumentException         => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status422UnprocessableEntity,
            _                         => StatusCodes.Status500InternalServerError
        };

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = ReasonPhrase(status), Detail = ex.Message }, ct);
        return true;
    }

    private static string ReasonPhrase(int status) => status switch
    {
        400 => "Bad request", 422 => "Unprocessable request", _ => "Internal server error"
    };
}
