namespace AgendaFlow.Domain.Exceptions;

public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }
}

public class AppointmentConflictException : DomainException
{
    public AppointmentConflictException()
        : base("APPOINTMENT_CONFLICT", "The requested time slot is no longer available.") { }
}

public class InvalidStatusTransitionException : DomainException
{
    public InvalidStatusTransitionException(string from, string to)
        : base("INVALID_STATUS_TRANSITION", $"Cannot transition appointment from {from} to {to}.") { }
}

public class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message)
        : base("BUSINESS_RULE_VIOLATION", message) { }
}

public class TenantNotFoundException : DomainException
{
    public TenantNotFoundException(string slug)
        : base("TENANT_NOT_FOUND", $"No active tenant found for slug '{slug}'.") { }
}

public class PlanLimitExceededException : DomainException
{
    public PlanLimitExceededException(string resource)
        : base("PLAN_LIMIT_EXCEEDED", $"Your current plan does not allow adding more {resource}.") { }
}
