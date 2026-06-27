using AgendaFlow.Domain.Entities;
using AgendaFlow.Domain.Enums;

namespace AgendaFlow.Application.Contracts;

// ── Tenant context ──────────────────────────────────────────────

/// <summary>
/// Provides the authenticated tenant resolved from the user's session.
/// Never trust a TenantId that comes from the request body or headers.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    bool HasTenant { get; }
}

// ── Repositories ────────────────────────────────────────────────

public interface ITenantRepository
{
    Task<Tenant?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<Tenant?> FindBySlugAsync(string slug, CancellationToken ct = default);
    Task AddAsync(Tenant tenant, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
}

public interface IServiceRepository
{
    Task<Service?> FindByIdAsync(Guid tenantId, Guid serviceId, CancellationToken ct = default);
    Task<IReadOnlyList<Service>> ListAsync(Guid tenantId, bool? activeOnly = null, CancellationToken ct = default);
    Task<int> CountActiveAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(Service service, CancellationToken ct = default);
}

public interface IProfessionalRepository
{
    Task<Professional?> FindByIdAsync(Guid tenantId, Guid professionalId, CancellationToken ct = default);
    Task<IReadOnlyList<Professional>> ListAsync(Guid tenantId, bool? activeOnly = null, CancellationToken ct = default);
    Task<int> CountActiveAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(Professional professional, CancellationToken ct = default);
}

public interface ICustomerRepository
{
    Task<Customer?> FindByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);
    Task<Customer?> FindByEmailAsync(Guid tenantId, string email, CancellationToken ct = default);
    Task AddAsync(Customer customer, CancellationToken ct = default);
}

public interface IAppointmentRepository
{
    Task<Appointment?> FindByIdAsync(Guid tenantId, Guid appointmentId, CancellationToken ct = default);
    Task<PagedResult<Appointment>> ListAsync(AppointmentFilter filter, CancellationToken ct = default);
    Task<int> CountForMonthAsync(Guid tenantId, int year, int month, CancellationToken ct = default);
    Task AddAsync(Appointment appointment, CancellationToken ct = default);
    Task<Appointment?> FindByIdempotencyKeyAsync(string key, CancellationToken ct = default);
}

public interface IBookingConfirmationRepository
{
    Task<BookingConfirmation?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task AddAsync(BookingConfirmation confirmation, CancellationToken ct = default);
}

public interface IAvailabilityRepository
{
    Task<IReadOnlyList<AvailabilityRule>> GetRulesAsync(Guid tenantId, Guid professionalId, CancellationToken ct = default);
    Task<IReadOnlyList<AvailabilityException>> GetExceptionsAsync(Guid tenantId, Guid professionalId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task AddRuleAsync(AvailabilityRule rule, CancellationToken ct = default);
    Task AddExceptionAsync(AvailabilityException exception, CancellationToken ct = default);
    Task DeleteRuleAsync(Guid tenantId, Guid ruleId, CancellationToken ct = default);
    Task DeleteExceptionAsync(Guid tenantId, Guid exceptionId, CancellationToken ct = default);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken ct = default);
    Task<PagedResult<AuditLog>> ListAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
}

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct = default);
}

// ── Membership ──────────────────────────────────────────────────

public interface IMembershipRepository
{
    Task<TenantMembership?> FindAsync(Guid tenantId, string userId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantMembership>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantMembership>> ListByUserAsync(string userId, CancellationToken ct = default);
    Task AddAsync(TenantMembership membership, CancellationToken ct = default);
}

// ── Unit of work ────────────────────────────────────────────────

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}

// ── Plan limits ─────────────────────────────────────────────────

public interface IPlanLimitService
{
    Task<bool> CanAddProfessionalAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> CanAddServiceAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> CanCreateAppointmentAsync(Guid tenantId, CancellationToken ct = default);
}

// ── Email ────────────────────────────────────────────────────────

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

public record EmailMessage(string To, string Subject, string HtmlBody);

// ── Clock abstraction ────────────────────────────────────────────

public interface IClock
{
    DateTime UtcNow { get; }
    DateTimeOffset UtcNowOffset { get; }
}

// ── Pagination ───────────────────────────────────────────────────

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

// ── Filters ──────────────────────────────────────────────────────

public record AppointmentFilter(
    Guid TenantId,
    Guid? ProfessionalId = null,
    Guid? ServiceId = null,
    AppointmentStatus? Status = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 25);
