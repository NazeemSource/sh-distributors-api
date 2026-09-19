using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Distributor.Api.Services;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title) = exception switch
        {
            BusinessException business => (business.Status, business.Code, business.Message),
            DbUpdateException => (StatusCodes.Status409Conflict, "database_conflict", "The request conflicts with existing data."),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "invalid_request", "The request is invalid."),
            _ => (StatusCodes.Status500InternalServerError, "server_error", "An unexpected error occurred.")
        };

        if (status >= 500) logger.LogError(exception, "Unhandled API error for {Method} {Path}", context.Request.Method, context.Request.Path);
        else logger.LogWarning("API request failed with {Code}: {Message}", code, exception.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://shdistrapi.umigs.com/errors/{code}",
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (environment.IsDevelopment() && exception is not BusinessException)
            problem.Extensions["developmentMessage"] = exception.Message;

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
