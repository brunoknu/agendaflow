namespace AgendaFlow.Domain.Enums;

public enum TenantPlan
{
    Free = 0,
    Pro = 1
}

public enum TenantStatus
{
    Active = 0,
    Suspended = 1,
    Cancelled = 2
}

public enum MembershipRole
{
    Owner = 0,
    Manager = 1,
    Staff = 2
}

public enum MembershipStatus
{
    Active = 0,
    Inactive = 1,
    Invited = 2
}

public enum AppointmentSource
{
    Public = 0,
    Internal = 1
}

public enum AvailabilityExceptionType
{
    DayOff = 0,
    SpecialHours = 1,
    PartialBlock = 2
}

public enum BookingConfirmationPurpose
{
    ConfirmAppointment = 0,
    CancelAppointment = 1
}

public enum OutboxMessageStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3,
    Dead = 4
}
