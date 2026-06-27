using AgendaFlow.Application.Contracts;
using AgendaFlow.Application.Tenants;
using AgendaFlow.Infrastructure.Email;
using AgendaFlow.Infrastructure.Identity;
using AgendaFlow.Infrastructure.Outbox;
using AgendaFlow.Infrastructure.Persistence;
using AgendaFlow.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgendaFlow.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Database ──────────────────────────────────────────────
        services.AddDbContext<AppDbContext>((sp, opts) =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not configured.");

            opts.UseNpgsql(connectionString, npg =>
            {
                npg.EnableRetryOnFailure(maxRetryCount: 3);
                npg.CommandTimeout(30);
            });

            // Enable in Development only (do not log in Production — may expose data)
            // opts.EnableDetailedErrors();
        });

        // ── Identity ──────────────────────────────────────────────
        services.AddIdentity<ApplicationUser, IdentityRole>(opts =>
        {
            opts.Password.RequiredLength = 8;
            opts.Password.RequireDigit = true;
            opts.Password.RequireLowercase = true;
            opts.Password.RequireUppercase = false;
            opts.Password.RequireNonAlphanumeric = false;
            opts.Lockout.MaxFailedAccessAttempts = 5;
            opts.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            opts.SignIn.RequireConfirmedEmail = true;
            // Anti-enumeration: return same message whether email exists or not
            opts.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        // ── Tenant context (scoped per request) ──────────────────
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        // ── Repositories ─────────────────────────────────────────
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IServiceRepository, ServiceRepository>();
        services.AddScoped<IProfessionalRepository, ProfessionalRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IAvailabilityRepository, AvailabilityRepository>();
        services.AddScoped<IBookingConfirmationRepository, BookingConfirmationRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();

        // ── Unit of work ─────────────────────────────────────────
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Application services ──────────────────────────────────
        services.AddScoped<IPlanLimitService, PlanLimitService>();

        // ── Clock ─────────────────────────────────────────────────
        services.AddSingleton<IClock, SystemClock>();

        // ── Email ─────────────────────────────────────────────────
        var emailOptions = configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
        services.AddSingleton(emailOptions);
        services.AddScoped<IEmailSender, MailKitEmailSender>();

        // ── Background services ───────────────────────────────────
        services.AddHostedService<OutboxProcessor>();

        return services;
    }
}

internal sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTimeOffset UtcNowOffset => DateTimeOffset.UtcNow;
}
