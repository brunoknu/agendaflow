using AgendaFlow.Domain.Enums;

namespace AgendaFlow.Domain.Entities;

public class ProfessionalService
{
    public Guid ProfessionalId { get; private set; }
    public Guid ServiceId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Professional? Professional { get; private set; }
    public Service? Service { get; private set; }

    private ProfessionalService() { }

    public static ProfessionalService Create(Guid professionalId, Guid serviceId)
        => new()
        {
            ProfessionalId = professionalId,
            ServiceId = serviceId,
            CreatedAtUtc = DateTime.UtcNow
        };
}

public class AvailabilityRule
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProfessionalId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly StartLocalTime { get; private set; }
    public TimeOnly EndLocalTime { get; private set; }
    public int SlotIntervalMinutes { get; private set; }
    public bool IsActive { get; private set; }

    public Professional? Professional { get; private set; }

    private AvailabilityRule() { }

    public static AvailabilityRule Create(
        Guid tenantId,
        Guid professionalId,
        DayOfWeek dayOfWeek,
        TimeOnly startLocalTime,
        TimeOnly endLocalTime,
        int slotIntervalMinutes = 30)
    {
        if (endLocalTime <= startLocalTime)
            throw new ArgumentException("End time must be after start time.");
        if (slotIntervalMinutes <= 0)
            throw new ArgumentException("Slot interval must be positive.");

        return new AvailabilityRule
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ProfessionalId = professionalId,
            DayOfWeek = dayOfWeek,
            StartLocalTime = startLocalTime,
            EndLocalTime = endLocalTime,
            SlotIntervalMinutes = slotIntervalMinutes,
            IsActive = true
        };
    }

    public void Deactivate() => IsActive = false;
}

public class AvailabilityException
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProfessionalId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly? StartLocalTime { get; private set; }
    public TimeOnly? EndLocalTime { get; private set; }
    public AvailabilityExceptionType Type { get; private set; }
    public string? Reason { get; private set; }

    public Professional? Professional { get; private set; }

    private AvailabilityException() { }

    public static AvailabilityException CreateDayOff(Guid tenantId, Guid professionalId, DateOnly date, string? reason = null)
        => new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ProfessionalId = professionalId,
            Date = date,
            Type = AvailabilityExceptionType.DayOff,
            Reason = reason
        };

    public static AvailabilityException CreateSpecialHours(
        Guid tenantId,
        Guid professionalId,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        string? reason = null)
        => new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ProfessionalId = professionalId,
            Date = date,
            StartLocalTime = startTime,
            EndLocalTime = endTime,
            Type = AvailabilityExceptionType.SpecialHours,
            Reason = reason
        };

    public bool IsFullDayOff => Type == AvailabilityExceptionType.DayOff;
}
