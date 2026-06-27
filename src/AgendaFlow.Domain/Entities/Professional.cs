namespace AgendaFlow.Domain.Entities;

public class Professional
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    // Navigation
    public Tenant? Tenant { get; private set; }
    public ICollection<ProfessionalService> ProfessionalServices { get; private set; } = [];
    public ICollection<AvailabilityRule> AvailabilityRules { get; private set; } = [];
    public ICollection<AvailabilityException> AvailabilityExceptions { get; private set; } = [];

    private Professional() { }

    public static Professional Create(Guid tenantId, string name, string? email, string? phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var now = DateTime.UtcNow;
        return new Professional
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Name = name.Trim(),
            Email = email?.Trim().ToLowerInvariant(),
            Phone = phone?.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Update(string name, string? email, string? phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Email = email?.Trim().ToLowerInvariant();
        Phone = phone?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Activate() { IsActive = true; UpdatedAtUtc = DateTime.UtcNow; }
    public void Deactivate() { IsActive = false; UpdatedAtUtc = DateTime.UtcNow; }

    public bool OffersService(Guid serviceId)
        => ProfessionalServices.Any(ps => ps.ServiceId == serviceId);
}
