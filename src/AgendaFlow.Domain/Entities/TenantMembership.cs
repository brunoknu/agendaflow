using AgendaFlow.Domain.Enums;

namespace AgendaFlow.Domain.Entities;

public class TenantMembership
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public MembershipRole Role { get; private set; }
    public MembershipStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // Navigation
    public Tenant? Tenant { get; private set; }

    private TenantMembership() { }

    public static TenantMembership Create(Guid tenantId, string userId, MembershipRole role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return new TenantMembership
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            UserId = userId,
            Role = role,
            Status = MembershipStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void UpdateRole(MembershipRole newRole)
    {
        Role = newRole;
    }

    public void Deactivate()
    {
        Status = MembershipStatus.Inactive;
    }
}
