using AgendaFlow.Application.Contracts;
using AgendaFlow.Domain.Entities;
using AgendaFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AgendaFlow.Infrastructure.Persistence.Repositories;

public sealed class TenantRepository : ITenantRepository
{
    private readonly AppDbContext _db;
    public TenantRepository(AppDbContext db) => _db = db;

    public Task<Tenant?> FindByIdAsync(Guid id, CancellationToken ct)
        => _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tenant?> FindBySlugAsync(string slug, CancellationToken ct)
        => _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public async Task AddAsync(Tenant tenant, CancellationToken ct)
        => await _db.Tenants.AddAsync(tenant, ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct)
        => _db.Tenants.AnyAsync(t => t.Slug == slug, ct);
}

public sealed class ServiceRepository : IServiceRepository
{
    private readonly AppDbContext _db;
    public ServiceRepository(AppDbContext db) => _db = db;

    public Task<Service?> FindByIdAsync(Guid tenantId, Guid serviceId, CancellationToken ct)
        => _db.Services.FirstOrDefaultAsync(s => s.Id == serviceId, ct); // global filter handles tenantId

    public async Task<IReadOnlyList<Service>> ListAsync(Guid tenantId, bool? activeOnly, CancellationToken ct)
    {
        var query = _db.Services.AsQueryable();
        if (activeOnly.HasValue)
            query = query.Where(s => s.IsActive == activeOnly.Value);
        return await query.OrderBy(s => s.Name).ToListAsync(ct);
    }

    public Task<int> CountActiveAsync(Guid tenantId, CancellationToken ct)
        => _db.Services.CountAsync(s => s.IsActive, ct);

    public async Task AddAsync(Service service, CancellationToken ct)
        => await _db.Services.AddAsync(service, ct);
}

public sealed class ProfessionalRepository : IProfessionalRepository
{
    private readonly AppDbContext _db;
    public ProfessionalRepository(AppDbContext db) => _db = db;

    public Task<Professional?> FindByIdAsync(Guid tenantId, Guid professionalId, CancellationToken ct)
        => _db.Professionals
            .Include(p => p.ProfessionalServices)
            .FirstOrDefaultAsync(p => p.Id == professionalId, ct);

    public async Task<IReadOnlyList<Professional>> ListAsync(Guid tenantId, bool? activeOnly, CancellationToken ct)
    {
        var query = _db.Professionals.Include(p => p.ProfessionalServices).AsQueryable();
        if (activeOnly.HasValue)
            query = query.Where(p => p.IsActive == activeOnly.Value);
        return await query.OrderBy(p => p.Name).ToListAsync(ct);
    }

    public Task<int> CountActiveAsync(Guid tenantId, CancellationToken ct)
        => _db.Professionals.CountAsync(p => p.IsActive, ct);

    public async Task AddAsync(Professional professional, CancellationToken ct)
        => await _db.Professionals.AddAsync(professional, ct);
}

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _db;
    public CustomerRepository(AppDbContext db) => _db = db;

    public Task<Customer?> FindByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct)
        => _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);

    public Task<Customer?> FindByEmailAsync(Guid tenantId, string email, CancellationToken ct)
        => _db.Customers.FirstOrDefaultAsync(c => c.Email == email, ct);

    public async Task AddAsync(Customer customer, CancellationToken ct)
        => await _db.Customers.AddAsync(customer, ct);
}

public sealed class AppointmentRepository : IAppointmentRepository
{
    private readonly AppDbContext _db;
    public AppointmentRepository(AppDbContext db) => _db = db;

    public Task<Appointment?> FindByIdAsync(Guid tenantId, Guid appointmentId, CancellationToken ct)
        => _db.Appointments
            .Include(a => a.Professional)
            .Include(a => a.Service)
            .Include(a => a.Customer)
            .Include(a => a.StatusHistory)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct);

    public async Task<PagedResult<Appointment>> ListAsync(AppointmentFilter filter, CancellationToken ct)
    {
        var query = _db.Appointments
            .Include(a => a.Professional)
            .Include(a => a.Service)
            .Include(a => a.Customer)
            .AsQueryable();

        if (filter.ProfessionalId.HasValue)
            query = query.Where(a => a.ProfessionalId == filter.ProfessionalId.Value);
        if (filter.ServiceId.HasValue)
            query = query.Where(a => a.ServiceId == filter.ServiceId.Value);
        if (filter.Status.HasValue)
            query = query.Where(a => a.Status == filter.Status.Value);
        if (filter.From.HasValue)
            query = query.Where(a => a.StartAtUtc >= filter.From.Value);
        if (filter.To.HasValue)
            query = query.Where(a => a.StartAtUtc <= filter.To.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.StartAtUtc)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Appointment>(items, total, filter.Page, filter.PageSize);
    }

    public Task<int> CountForMonthAsync(Guid tenantId, int year, int month, CancellationToken ct)
    {
        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);
        return _db.Appointments.CountAsync(
            a => a.CreatedAtUtc >= from && a.CreatedAtUtc < to
              && a.Status != AppointmentStatus.Cancelled, ct);
    }

    public async Task AddAsync(Appointment appointment, CancellationToken ct)
        => await _db.Appointments.AddAsync(appointment, ct);

    public Task<Appointment?> FindByIdempotencyKeyAsync(string key, CancellationToken ct)
    {
        // Idempotency keys stored as a shadow property or a separate table
        // For simplicity we could use the AuditLog to check, but here we
        // rely on the caller to pass a determistic key derived from their request
        // This is a stub — a real implementation would have a dedicated table
        return Task.FromResult<Appointment?>(null);
    }
}

public sealed class AvailabilityRepository : IAvailabilityRepository
{
    private readonly AppDbContext _db;
    public AvailabilityRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<AvailabilityRule>> GetRulesAsync(Guid tenantId, Guid professionalId, CancellationToken ct)
        => await _db.AvailabilityRules
            .Where(r => r.ProfessionalId == professionalId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AvailabilityException>> GetExceptionsAsync(
        Guid tenantId, Guid professionalId, DateOnly from, DateOnly to, CancellationToken ct)
        => await _db.AvailabilityExceptions
            .Where(e => e.ProfessionalId == professionalId && e.Date >= from && e.Date <= to)
            .ToListAsync(ct);

    public async Task AddRuleAsync(AvailabilityRule rule, CancellationToken ct)
        => await _db.AvailabilityRules.AddAsync(rule, ct);

    public async Task AddExceptionAsync(AvailabilityException exception, CancellationToken ct)
        => await _db.AvailabilityExceptions.AddAsync(exception, ct);

    public async Task DeleteRuleAsync(Guid tenantId, Guid ruleId, CancellationToken ct)
    {
        var rule = await _db.AvailabilityRules.FirstOrDefaultAsync(r => r.Id == ruleId, ct);
        if (rule is not null) _db.AvailabilityRules.Remove(rule);
    }

    public async Task DeleteExceptionAsync(Guid tenantId, Guid exceptionId, CancellationToken ct)
    {
        var ex = await _db.AvailabilityExceptions.FirstOrDefaultAsync(e => e.Id == exceptionId, ct);
        if (ex is not null) _db.AvailabilityExceptions.Remove(ex);
    }
}

public sealed class BookingConfirmationRepository : IBookingConfirmationRepository
{
    private readonly AppDbContext _db;
    public BookingConfirmationRepository(AppDbContext db) => _db = db;

    public Task<BookingConfirmation?> FindByTokenHashAsync(string tokenHash, CancellationToken ct)
        => _db.BookingConfirmations
            .Include(bc => bc.Appointment)
            .FirstOrDefaultAsync(bc => bc.TokenHash == tokenHash, ct);

    public async Task AddAsync(BookingConfirmation confirmation, CancellationToken ct)
        => await _db.BookingConfirmations.AddAsync(confirmation, ct);
}

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly AppDbContext _db;
    public OutboxRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(OutboxMessage message, CancellationToken ct)
        => await _db.OutboxMessages.AddAsync(message, ct);

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken ct)
        => await _db.OutboxMessages
            .Where(m => m.Status == AgendaFlow.Domain.Enums.OutboxMessageStatus.Pending
                     && m.NextAttemptAtUtc <= DateTime.UtcNow)
            .OrderBy(m => m.NextAttemptAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);
}

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _db;
    public AuditLogRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(AuditLog log, CancellationToken ct)
        => await _db.AuditLogs.AddAsync(log, ct);

    public async Task<PagedResult<AuditLog>> ListAsync(Guid tenantId, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.AuditLogs.Where(l => l.TenantId == tenantId).OrderByDescending(l => l.OccurredAtUtc);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<AuditLog>(items, total, page, pageSize);
    }
}

public sealed class MembershipRepository : IMembershipRepository
{
    private readonly AppDbContext _db;
    public MembershipRepository(AppDbContext db) => _db = db;

    public Task<TenantMembership?> FindAsync(Guid tenantId, string userId, CancellationToken ct)
        => _db.TenantMemberships.FirstOrDefaultAsync(m => m.UserId == userId, ct);

    public async Task<IReadOnlyList<TenantMembership>> ListByTenantAsync(Guid tenantId, CancellationToken ct)
        => await _db.TenantMemberships.ToListAsync(ct);

    public async Task<IReadOnlyList<TenantMembership>> ListByUserAsync(string userId, CancellationToken ct)
        => await _db.TenantMemberships
            .Where(m => m.UserId == userId && m.Status == Domain.Enums.MembershipStatus.Active)
            .Include(m => m.Tenant)
            .ToListAsync(ct);

    public async Task AddAsync(TenantMembership membership, CancellationToken ct)
        => await _db.TenantMemberships.AddAsync(membership, ct);
}
