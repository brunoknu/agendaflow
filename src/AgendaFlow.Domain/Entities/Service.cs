using AgendaFlow.Domain.Exceptions;

namespace AgendaFlow.Domain.Entities;

public class Service
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int DurationMinutes { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public int BufferBeforeMinutes { get; private set; }
    public int BufferAfterMinutes { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    // Navigation
    public Tenant? Tenant { get; private set; }
    public ICollection<ProfessionalService> ProfessionalServices { get; private set; } = [];

    private Service() { }

    public static Service Create(
        Guid tenantId,
        string name,
        string? description,
        int durationMinutes,
        decimal price,
        string currency = "BRL",
        int bufferBeforeMinutes = 0,
        int bufferAfterMinutes = 0)
    {
        if (durationMinutes <= 0)
            throw new BusinessRuleViolationException("Service duration must be greater than zero.");
        if (durationMinutes > 480)
            throw new BusinessRuleViolationException("Service duration cannot exceed 8 hours.");
        if (price < 0)
            throw new BusinessRuleViolationException("Service price cannot be negative.");
        if (bufferBeforeMinutes < 0 || bufferAfterMinutes < 0)
            throw new BusinessRuleViolationException("Buffer times cannot be negative.");

        var now = DateTime.UtcNow;
        return new Service
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            DurationMinutes = durationMinutes,
            Price = price,
            Currency = currency.ToUpperInvariant(),
            BufferBeforeMinutes = bufferBeforeMinutes,
            BufferAfterMinutes = bufferAfterMinutes,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Update(string name, string? description, int durationMinutes, decimal price, int bufferBefore, int bufferAfter)
    {
        if (durationMinutes <= 0)
            throw new BusinessRuleViolationException("Service duration must be greater than zero.");
        if (price < 0)
            throw new BusinessRuleViolationException("Service price cannot be negative.");

        Name = name.Trim();
        Description = description?.Trim();
        DurationMinutes = durationMinutes;
        Price = price;
        BufferBeforeMinutes = bufferBefore;
        BufferAfterMinutes = bufferAfter;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Activate() { IsActive = true; UpdatedAtUtc = DateTime.UtcNow; }
    public void Deactivate() { IsActive = false; UpdatedAtUtc = DateTime.UtcNow; }

    /// <summary>
    /// Total time this service blocks on the calendar, including buffers.
    /// </summary>
    public int TotalBlockMinutes => BufferBeforeMinutes + DurationMinutes + BufferAfterMinutes;
}
