using AgendaFlow.Application.Contracts;
using AgendaFlow.Domain.Enums;

namespace AgendaFlow.Application.Tenants;

/// <summary>
/// Enforces per-plan resource limits on the server side.
/// The frontend may show warnings but limits are enforced here.
/// </summary>
public sealed class PlanLimitService : IPlanLimitService
{
    private static readonly Dictionary<TenantPlan, PlanLimits> Limits = new()
    {
        [TenantPlan.Free] = new(MaxProfessionals: 1, MaxActiveServices: 3, MaxMonthlyAppointments: 50),
        [TenantPlan.Pro] = new(MaxProfessionals: 20, MaxActiveServices: 50, MaxMonthlyAppointments: 500)
    };

    private readonly ITenantRepository _tenants;
    private readonly IProfessionalRepository _professionals;
    private readonly IServiceRepository _services;
    private readonly IAppointmentRepository _appointments;

    public PlanLimitService(
        ITenantRepository tenants,
        IProfessionalRepository professionals,
        IServiceRepository services,
        IAppointmentRepository appointments)
    {
        _tenants = tenants;
        _professionals = professionals;
        _services = services;
        _appointments = appointments;
    }

    public async Task<bool> CanAddProfessionalAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _tenants.FindByIdAsync(tenantId, ct) ?? throw new InvalidOperationException("Tenant not found.");
        var limit = Limits[tenant.Plan];
        var current = await _professionals.CountActiveAsync(tenantId, ct);
        return current < limit.MaxProfessionals;
    }

    public async Task<bool> CanAddServiceAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _tenants.FindByIdAsync(tenantId, ct) ?? throw new InvalidOperationException("Tenant not found.");
        var limit = Limits[tenant.Plan];
        var current = await _services.CountActiveAsync(tenantId, ct);
        return current < limit.MaxActiveServices;
    }

    public async Task<bool> CanCreateAppointmentAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _tenants.FindByIdAsync(tenantId, ct) ?? throw new InvalidOperationException("Tenant not found.");
        var limit = Limits[tenant.Plan];
        var now = DateTime.UtcNow;
        var current = await _appointments.CountForMonthAsync(tenantId, now.Year, now.Month, ct);
        return current < limit.MaxMonthlyAppointments;
    }

    private record PlanLimits(int MaxProfessionals, int MaxActiveServices, int MaxMonthlyAppointments);
}
