using AgendaFlow.Domain.Entities;
using AgendaFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgendaFlow.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(100).IsRequired();
        builder.Property(t => t.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Plan).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(50);
        builder.HasIndex(t => t.Slug).IsUnique();
    }
}

public class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("tenant_memberships");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.UserId).HasMaxLength(450).IsRequired();
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(50);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(50);
        // Prevent duplicate memberships: same user can only belong to a tenant once
        builder.HasIndex(m => new { m.TenantId, m.UserId }).IsUnique();
        builder.HasOne(m => m.Tenant).WithMany(t => t.Memberships).HasForeignKey(m => m.TenantId);
    }
}

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("services");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(1000);
        builder.Property(s => s.Price).HasPrecision(10, 2);
        builder.Property(s => s.Currency).HasMaxLength(3);
        builder.HasIndex(s => s.TenantId);
        builder.HasOne(s => s.Tenant).WithMany(t => t.Services).HasForeignKey(s => s.TenantId);
    }
}

public class ProfessionalConfiguration : IEntityTypeConfiguration<Professional>
{
    public void Configure(EntityTypeBuilder<Professional> builder)
    {
        builder.ToTable("professionals");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Email).HasMaxLength(256);
        builder.Property(p => p.Phone).HasMaxLength(50);
        builder.HasIndex(p => p.TenantId);
        builder.HasOne(p => p.Tenant).WithMany(t => t.Professionals).HasForeignKey(p => p.TenantId);
    }
}

public class ProfessionalServiceConfiguration : IEntityTypeConfiguration<ProfessionalService>
{
    public void Configure(EntityTypeBuilder<ProfessionalService> builder)
    {
        builder.ToTable("professional_services");
        builder.HasKey(ps => new { ps.ProfessionalId, ps.ServiceId });
        builder.HasOne(ps => ps.Professional).WithMany(p => p.ProfessionalServices).HasForeignKey(ps => ps.ProfessionalId);
        builder.HasOne(ps => ps.Service).WithMany(s => s.ProfessionalServices).HasForeignKey(ps => ps.ServiceId);
    }
}

public class AvailabilityRuleConfiguration : IEntityTypeConfiguration<AvailabilityRule>
{
    public void Configure(EntityTypeBuilder<AvailabilityRule> builder)
    {
        builder.ToTable("availability_rules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.DayOfWeek).HasConversion<int>();
        builder.Property(r => r.StartLocalTime).HasColumnType("time");
        builder.Property(r => r.EndLocalTime).HasColumnType("time");
        builder.HasIndex(r => new { r.TenantId, r.ProfessionalId });
        builder.HasOne(r => r.Professional).WithMany(p => p.AvailabilityRules).HasForeignKey(r => r.ProfessionalId);
    }
}

public class AvailabilityExceptionConfiguration : IEntityTypeConfiguration<AvailabilityException>
{
    public void Configure(EntityTypeBuilder<AvailabilityException> builder)
    {
        builder.ToTable("availability_exceptions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.StartLocalTime).HasColumnType("time");
        builder.Property(e => e.EndLocalTime).HasColumnType("time");
        builder.HasIndex(e => new { e.TenantId, e.ProfessionalId, e.Date });
        builder.HasOne(e => e.Professional).WithMany(p => p.AvailabilityExceptions).HasForeignKey(e => e.ProfessionalId);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.Phone).HasMaxLength(50);
        // Avoid duplicates: one email per tenant
        builder.HasIndex(c => new { c.TenantId, c.Email }).IsUnique()
            .HasFilter("\"Email\" IS NOT NULL");
    }
}

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Source).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Notes).HasMaxLength(500);

        // Optimistic concurrency
        builder.Property(a => a.Version).IsRowVersion();

        // Indexes for common queries
        builder.HasIndex(a => new { a.TenantId, a.StartAtUtc });
        builder.HasIndex(a => new { a.TenantId, a.ProfessionalId, a.Status });

        // Navigation
        builder.HasOne(a => a.Professional).WithMany().HasForeignKey(a => a.ProfessionalId);
        builder.HasOne(a => a.Service).WithMany().HasForeignKey(a => a.ServiceId);
        builder.HasOne(a => a.Customer).WithMany().HasForeignKey(a => a.CustomerId);

        // NOTE: The PostgreSQL exclusion constraint preventing double-booking
        // is added via a raw migration (see: Migrations/AddAppointmentExclusionConstraint.sql)
        // It enforces: no two ACTIVE appointments for the same professional can overlap
        // on their blocked time ranges.
        // Statuses that occupy the schedule: PendingConfirmation, Confirmed, CheckedIn
    }
}

public class AppointmentStatusHistoryConfiguration : IEntityTypeConfiguration<AppointmentStatusHistory>
{
    public void Configure(EntityTypeBuilder<AppointmentStatusHistory> builder)
    {
        builder.ToTable("appointment_status_history");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.PreviousStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.NewStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.ChangedByUserId).HasMaxLength(450);
        builder.Property(h => h.Reason).HasMaxLength(500);
        builder.HasIndex(h => h.AppointmentId);
        builder.HasOne(h => h.Appointment).WithMany(a => a.StatusHistory).HasForeignKey(h => h.AppointmentId);
    }
}

public class BookingConfirmationConfiguration : IEntityTypeConfiguration<BookingConfirmation>
{
    public void Configure(EntityTypeBuilder<BookingConfirmation> builder)
    {
        builder.ToTable("booking_confirmations");
        builder.HasKey(bc => bc.Id);
        builder.Property(bc => bc.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(bc => bc.Purpose).HasConversion<string>().HasMaxLength(50);
        // TokenHash must be unique and indexed for fast lookups
        builder.HasIndex(bc => bc.TokenHash).IsUnique();
        builder.HasOne(bc => bc.Appointment).WithMany().HasForeignKey(bc => bc.AppointmentId);
    }
}

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Type).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(m => m.LastError).HasMaxLength(2000);
        builder.HasIndex(m => new { m.Status, m.NextAttemptAtUtc });
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasMaxLength(200).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(200).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(a => a.CorrelationId).HasMaxLength(100);
        builder.Property(a => a.ActorUserId).HasMaxLength(450);
        // Large metadata stored as jsonb in PostgreSQL for efficient querying
        builder.Property(a => a.Metadata).HasColumnType("jsonb");
        builder.HasIndex(a => new { a.TenantId, a.OccurredAtUtc });
        builder.HasIndex(a => a.CorrelationId);
    }
}
