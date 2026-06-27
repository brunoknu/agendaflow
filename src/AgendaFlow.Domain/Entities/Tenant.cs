using AgendaFlow.Domain.Enums;

namespace AgendaFlow.Domain.Entities;

public class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string TimeZoneId { get; private set; } = "UTC";
    public TenantPlan Plan { get; private set; }
    public TenantStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    // Navigation
    public ICollection<TenantMembership> Memberships { get; private set; } = [];
    public ICollection<Service> Services { get; private set; } = [];
    public ICollection<Professional> Professionals { get; private set; } = [];

    private Tenant() { }

    public static Tenant Create(string name, string slug, string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        var now = DateTime.UtcNow;
        return new Tenant
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            TimeZoneId = timeZoneId,
            Plan = TenantPlan.Free,
            Status = TenantStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Update(string name, string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        Name = name.Trim();
        TimeZoneId = timeZoneId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public bool IsActive() => Status == TenantStatus.Active;
}
