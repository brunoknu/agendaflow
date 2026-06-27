using AgendaFlow.Application.Contracts;
using AgendaFlow.Infrastructure.Identity;
using AgendaFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgendaFlow.Api.Middleware;

/// <summary>
/// Resolves the current tenant from the authenticated user's session claims.
/// The TenantId is NEVER read from the request body or headers — only from the server-side session.
/// Must run after authentication middleware.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, AppDbContext db)
    {
        // Skip for public routes that don't need tenant context
        var path = context.Request.Path.Value ?? string.Empty;
        if (IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            await _next(context);
            return;
        }

        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            await _next(context);
            return;
        }

        // Resolve tenant from session (DB-backed, not from request)
        var membership = await db.TenantMemberships
            .Include(m => m.Tenant)
            .FirstOrDefaultAsync(m =>
                m.UserId == userId &&
                m.Status == Domain.Enums.MembershipStatus.Active &&
                m.Tenant!.Status == Domain.Enums.TenantStatus.Active,
            context.RequestAborted);

        if (membership is not null)
        {
            tenantContext.SetTenant(membership.TenantId);
        }

        await _next(context);
    }

    private static bool IsPublicPath(string path)
        => path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/public", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
