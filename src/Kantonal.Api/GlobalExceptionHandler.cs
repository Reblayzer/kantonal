using Kantonal.Application.Errors;
using Microsoft.AspNetCore.Diagnostics;

namespace Kantonal.Api;

/// <summary>Maps typed domain errors to HTTP status codes + the standard error envelope.</summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (status, code, message) = exception switch
        {
            NotFoundException nf => (StatusCodes.Status404NotFound, nf.Code, nf.Message),
            ValidationException ve => (StatusCodes.Status400BadRequest, ve.Code, ve.Message),
            _ => (StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred."),
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception processing {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(ApiEnvelope.Error(code, message), ct);
        return true;
    }
}
