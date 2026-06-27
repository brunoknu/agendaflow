namespace AgendaFlow.Domain.Enums;

public enum AppointmentStatus
{
    PendingConfirmation = 0,
    Confirmed = 1,
    CheckedIn = 2,
    Completed = 3,
    Cancelled = 4,
    NoShow = 5
}
