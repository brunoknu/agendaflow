using AgendaFlow.Application.Contracts;

namespace AgendaFlow.Infrastructure.Identity;

/// <summary>
/// Holds the current tenant resolved from the authenticated user's session.
/// Registered as Scoped — one instance per HTTP request.
/// The tenant is NEVER taken from the request body, query string, or headers.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private Guid? _tenantId;

    public Guid TenantId => _tenantId
        ?? throw new InvalidOperationException(
            "Tenant context has not been set. Ensure the tenant middleware has run.");

    public bool HasTenant => _tenantId.HasValue;

    public void SetTenant(Guid tenantId)
    {
        if (_tenantId.HasValue && _tenantId != tenantId)
            throw new InvalidOperationException("Tenant context cannot be changed within a request.");
        _tenantId = tenantId;
    }
}
