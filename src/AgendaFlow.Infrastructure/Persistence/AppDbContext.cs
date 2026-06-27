// Requires: Microsoft.EntityFrameworkCore, Npgsql.EntityFrameworkCore.PostgreSQL
// Install: dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL

using AgendaFlow.Application.Contracts;
using AgendaFlow.Domain.Entities;
using AgendaFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AgendaFlow.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // Tenant-scoped tables
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Professional> Professionals => Set<Professional>();
    public DbSet<ProfessionalService> ProfessionalServices => Set<ProfessionalService>();
    public DbSet<AvailabilityRule> AvailabilityRules => Set<AvailabilityRule>();
    public DbSet<AvailabilityException> AvailabilityExceptions => Set<AvailabilityException>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentStatusHistory> AppointmentStatusHistories => Set<AppointmentStatusHistory>();
    public DbSet<BookingConfirmation> BookingConfirmations => Set<BookingConfirmation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Global query filters for tenant isolation
        // Applied automatically on all queries — defense layer 2 (after tenant context)
        if (_tenantContext.HasTenant)
        {
            var tenantId = _tenantContext.TenantId;
            builder.Entity<Service>().HasQueryFilter(s => s.TenantId == tenantId);
            builder.Entity<Professional>().HasQueryFilter(p => p.TenantId == tenantId);
            builder.Entity<AvailabilityRule>().HasQueryFilter(r => r.TenantId == tenantId);
            builder.Entity<AvailabilityException>().HasQueryFilter(e => e.TenantId == tenantId);
            builder.Entity<Customer>().HasQueryFilter(c => c.TenantId == tenantId);
            builder.Entity<Appointment>().HasQueryFilter(a => a.TenantId == tenantId);
            builder.Entity<AppointmentStatusHistory>().HasQueryFilter(h => h.TenantId == tenantId);
            builder.Entity<BookingConfirmation>().HasQueryFilter(bc => bc.TenantId == tenantId);
            builder.Entity<TenantMembership>().HasQueryFilter(m => m.TenantId == tenantId);
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Enable detailed errors and sensitive data logging only in Development
        // Production config comes from environment variables
    }
}
