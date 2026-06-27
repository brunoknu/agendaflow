using AgendaFlow.Domain.Enums;

namespace AgendaFlow.Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public OutboxMessageStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(string type, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Type = type,
            Payload = payload,
            Status = OutboxMessageStatus.Pending,
            AttemptCount = 0,
            CreatedAtUtc = DateTime.UtcNow,
            NextAttemptAtUtc = DateTime.UtcNow
        };
    }

    public void MarkProcessing()
    {
        Status = OutboxMessageStatus.Processing;
        AttemptCount++;
    }

    public void MarkSent()
    {
        Status = OutboxMessageStatus.Sent;
        ProcessedAtUtc = DateTime.UtcNow;
        LastError = null;
    }

    public void MarkFailed(string error, int maxAttempts = 5)
    {
        LastError = error;
        if (AttemptCount >= maxAttempts)
        {
            Status = OutboxMessageStatus.Dead;
        }
        else
        {
            Status = OutboxMessageStatus.Pending;
            // Exponential backoff: 1m, 2m, 4m, 8m, 16m
            var delayMinutes = Math.Pow(2, AttemptCount - 1);
            NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(delayMinutes);
        }
    }
}

public class AuditLog
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string? ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public string? CorrelationId { get; private set; }

    /// <summary>Sanitized metadata — no passwords, tokens, or full PII payloads.</summary>
    public string? Metadata { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(
        Guid? tenantId,
        string? actorUserId,
        string action,
        string entityType,
        string entityId,
        string? correlationId = null,
        string? metadata = null)
        => new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = correlationId,
            Metadata = metadata
        };
}
