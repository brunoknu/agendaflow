using AgendaFlow.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AgendaFlow.Api.Middleware;

/// <summary>
/// Catches all unhandled exceptions and returns RFC 7807 Problem Details.
/// Never exposes stack traces or database errors to the client.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.TraceIdentifier;

        _logger.LogError(exception, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);

        var (statusCode, title, detail) = MapException(exception);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["correlationId"] = correlationId;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            }),
            context.RequestAborted);
    }

    private static (int status, string title, string detail) MapException(Exception ex)
        => ex switch
        {
            AppointmentConflictException e =>
                (StatusCodes.Status409Conflict, "Appointment Conflict", e.Message),

            InvalidStatusTransitionException e =>
                (StatusCodes.Status422UnprocessableEntity, "Invalid Status Transition", e.Message),

            PlanLimitExceededException e =>
                (StatusCodes.Status422UnprocessableEntity, "Plan Limit Exceeded", e.Message),

            BusinessRuleViolationException e =>
                (StatusCodes.Status422UnprocessableEntity, "Business Rule Violation", e.Message),

            TenantNotFoundException e =>
                (StatusCodes.Status404NotFound, "Tenant Not Found", e.Message),

            UnauthorizedAccessException =>
                (StatusCodes.Status403Forbidden, "Forbidden", "You do not have permission to perform this action."),

            // PostgreSQL exclusion constraint violation — do not expose DB details
            Microsoft.EntityFrameworkCore.DbUpdateException dbEx
                when dbEx.InnerException?.Message.Contains("exclusion") == true =>
                (StatusCodes.Status409Conflict, "Appointment Conflict",
                    "The requested time slot is no longer available."),

            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error",
                "An unexpected error occurred. Please try again later.")
        };
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionMiddleware>();
}
