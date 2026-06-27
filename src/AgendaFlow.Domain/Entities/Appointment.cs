using AgendaFlow.Domain.Enums;
using AgendaFlow.Domain.Exceptions;

namespace AgendaFlow.Domain.Entities;

public class Appointment
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProfessionalId { get; private set; }
    public Guid ServiceId { get; private set; }
    public Guid CustomerId { get; private set; }

    /// <summary>When the service itself starts (excludes buffer before).</summary>
    public DateTime StartAtUtc { get; private set; }

    /// <summary>When the service itself ends (excludes buffer after).</summary>
    public DateTime EndAtUtc { get; private set; }

    /// <summary>Calendar block start — includes buffer before.</summary>
    public DateTime BlockedStartAtUtc { get; private set; }

    /// <summary>Calendar block end — includes buffer after.</summary>
    public DateTime BlockedEndAtUtc { get; private set; }

    public AppointmentStatus Status { get; private set; }
    public AppointmentSource Source { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>Optimistic concurrency token.</summary>
    public uint Version { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    // Navigation
    public Tenant? Tenant { get; private set; }
    public Professional? Professional { get; private set; }
    public Service? Service { get; private set; }
    public Customer? Customer { get; private set; }
    public ICollection<AppointmentStatusHistory> StatusHistory { get; private set; } = [];

    private Appointment() { }

    public static Appointment Create(
        Guid tenantId,
        Guid professionalId,
        Guid serviceId,
        Guid customerId,
        DateTime startAtUtc,
        int durationMinutes,
        int bufferBeforeMinutes,
        int bufferAfterMinutes,
        AppointmentSource source,
        string? notes = null)
    {
        if (startAtUtc <= DateTime.UtcNow)
            throw new BusinessRuleViolationException("Appointments cannot be scheduled in the past.");

        var now = DateTime.UtcNow;
        var endAtUtc = startAtUtc.AddMinutes(durationMinutes);
        var blockedStart = startAtUtc.AddMinutes(-bufferBeforeMinutes);
        var blockedEnd = endAtUtc.AddMinutes(bufferAfterMinutes);

        if (notes?.Length > 500)
            throw new BusinessRuleViolationException("Notes cannot exceed 500 characters.");

        return new Appointment
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ProfessionalId = professionalId,
            ServiceId = serviceId,
            CustomerId = customerId,
            StartAtUtc = startAtUtc,
            EndAtUtc = endAtUtc,
            BlockedStartAtUtc = blockedStart,
            BlockedEndAtUtc = blockedEnd,
            Status = AppointmentStatus.PendingConfirmation,
            Source = source,
            Notes = notes?.Trim(),
            Version = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    // ----- Status machine -----

    private static readonly Dictionary<AppointmentStatus, IReadOnlySet<AppointmentStatus>> AllowedTransitions = new()
    {
        [AppointmentStatus.PendingConfirmation] = new HashSet<AppointmentStatus>
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.Cancelled
        },
        [AppointmentStatus.Confirmed] = new HashSet<AppointmentStatus>
        {
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Cancelled,
            AppointmentStatus.NoShow
        },
        [AppointmentStatus.CheckedIn] = new HashSet<AppointmentStatus>
        {
            AppointmentStatus.Completed
        },
        [AppointmentStatus.Completed] = new HashSet<AppointmentStatus>(),
        [AppointmentStatus.Cancelled] = new HashSet<AppointmentStatus>(),
        [AppointmentStatus.NoShow] = new HashSet<AppointmentStatus>()
    };

    public void Transition(AppointmentStatus newStatus, string? changedByUserId, string? reason = null)
    {
        if (!AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(newStatus))
            throw new InvalidStatusTransitionException(Status.ToString(), newStatus.ToString());

        var previous = Status;
        Status = newStatus;
        UpdatedAtUtc = DateTime.UtcNow;

        StatusHistory.Add(AppointmentStatusHistory.Create(
            Id, TenantId, previous, newStatus, changedByUserId, reason));
    }

    public bool CanBeCancelled() =>
        Status is AppointmentStatus.PendingConfirmation or AppointmentStatus.Confirmed;

    public bool CanBeRescheduled() =>
        Status is AppointmentStatus.PendingConfirmation or AppointmentStatus.Confirmed;

    public void Reschedule(
        DateTime newStartAtUtc,
        int durationMinutes,
        int bufferBeforeMinutes,
        int bufferAfterMinutes,
        string? changedByUserId)
    {
        if (!CanBeRescheduled())
            throw new BusinessRuleViolationException("This appointment cannot be rescheduled in its current state.");
        if (newStartAtUtc <= DateTime.UtcNow)
            throw new BusinessRuleViolationException("Cannot reschedule to a past time.");

        StartAtUtc = newStartAtUtc;
        EndAtUtc = newStartAtUtc.AddMinutes(durationMinutes);
        BlockedStartAtUtc = newStartAtUtc.AddMinutes(-bufferBeforeMinutes);
        BlockedEndAtUtc = EndAtUtc.AddMinutes(bufferAfterMinutes);
        UpdatedAtUtc = DateTime.UtcNow;

        // Rescheduled appointments go back to PendingConfirmation
        if (Status == AppointmentStatus.Confirmed)
        {
            var prev = Status;
            Status = AppointmentStatus.PendingConfirmation;
            StatusHistory.Add(AppointmentStatusHistory.Create(
                Id, TenantId, prev, AppointmentStatus.PendingConfirmation, changedByUserId, "Rescheduled"));
        }
    }
}

public class AppointmentStatusHistory
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid AppointmentId { get; private set; }
    public AppointmentStatus PreviousStatus { get; private set; }
    public AppointmentStatus NewStatus { get; private set; }
    public string? ChangedByUserId { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }
    public string? Reason { get; private set; }

    public Appointment? Appointment { get; private set; }

    private AppointmentStatusHistory() { }

    internal static AppointmentStatusHistory Create(
        Guid appointmentId,
        Guid tenantId,
        AppointmentStatus previous,
        AppointmentStatus next,
        string? changedByUserId,
        string? reason)
        => new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            AppointmentId = appointmentId,
            PreviousStatus = previous,
            NewStatus = next,
            ChangedByUserId = changedByUserId,
            ChangedAtUtc = DateTime.UtcNow,
            Reason = reason
        };
}

public class BookingConfirmation
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid AppointmentId { get; private set; }

    /// <summary>SHA-256 hash of the raw token. Never store the raw token.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public BookingConfirmationPurpose Purpose { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }

    public Appointment? Appointment { get; private set; }

    private BookingConfirmation() { }

    public static BookingConfirmation Create(
        Guid tenantId,
        Guid appointmentId,
        string tokenHash,
        BookingConfirmationPurpose purpose,
        DateTime expiresAtUtc)
        => new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            AppointmentId = appointmentId,
            TokenHash = tokenHash,
            Purpose = purpose,
            ExpiresAtUtc = expiresAtUtc
        };

    public bool IsValid() => UsedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;

    public void MarkAsUsed()
    {
        if (!IsValid())
            throw new BusinessRuleViolationException("This confirmation link is no longer valid.");
        UsedAtUtc = DateTime.UtcNow;
    }
}
