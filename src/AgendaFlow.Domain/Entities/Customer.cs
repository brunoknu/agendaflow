namespace AgendaFlow.Domain.Entities;

public class Customer
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public Tenant? Tenant { get; private set; }

    private Customer() { }

    public static Customer Create(Guid tenantId, string name, string? email, string? phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var now = DateTime.UtcNow;
        return new Customer
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Name = name.Trim(),
            Email = email?.Trim().ToLowerInvariant(),
            Phone = phone?.Trim(),
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
}
