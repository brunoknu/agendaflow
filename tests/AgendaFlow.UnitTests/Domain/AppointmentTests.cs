using AgendaFlow.Domain.Entities;
using AgendaFlow.Domain.Enums;
using AgendaFlow.Domain.Exceptions;
using Xunit;

namespace AgendaFlow.UnitTests.Domain;

public class AppointmentStatusMachineTests
{
    private static Appointment CreateFutureAppointment()
        => Appointment.Create(
            tenantId: Guid.NewGuid(),
            professionalId: Guid.NewGuid(),
            serviceId: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            startAtUtc: DateTime.UtcNow.AddHours(2),
            durationMinutes: 30,
            bufferBeforeMinutes: 0,
            bufferAfterMinutes: 0,
            source: AppointmentSource.Internal);

    [Fact]
    public void New_appointment_starts_as_PendingConfirmation()
    {
        var appt = CreateFutureAppointment();
        Assert.Equal(AppointmentStatus.PendingConfirmation, appt.Status);
    }

    [Theory]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Cancelled)]
    public void PendingConfirmation_can_transition_to_valid_statuses(AppointmentStatus next)
    {
        var appt = CreateFutureAppointment();
        appt.Transition(next, null);
        Assert.Equal(next, appt.Status);
    }

    [Theory]
    [InlineData(AppointmentStatus.CheckedIn)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    [InlineData(AppointmentStatus.PendingConfirmation)]
    public void PendingConfirmation_cannot_skip_to_invalid_statuses(AppointmentStatus next)
    {
        var appt = CreateFutureAppointment();
        Assert.Throws<InvalidStatusTransitionException>(() => appt.Transition(next, null));
    }

    [Fact]
    public void Confirmed_can_transition_to_CheckedIn()
    {
        var appt = CreateFutureAppointment();
        appt.Transition(AppointmentStatus.Confirmed, null);
        appt.Transition(AppointmentStatus.CheckedIn, null);
        Assert.Equal(AppointmentStatus.CheckedIn, appt.Status);
    }

    [Fact]
    public void CheckedIn_can_transition_to_Completed()
    {
        var appt = CreateFutureAppointment();
        appt.Transition(AppointmentStatus.Confirmed, null);
        appt.Transition(AppointmentStatus.CheckedIn, null);
        appt.Transition(AppointmentStatus.Completed, null);
        Assert.Equal(AppointmentStatus.Completed, appt.Status);
    }

    [Fact]
    public void Completed_cannot_transition_to_any_status()
    {
        var appt = CreateFutureAppointment();
        appt.Transition(AppointmentStatus.Confirmed, null);
        appt.Transition(AppointmentStatus.CheckedIn, null);
        appt.Transition(AppointmentStatus.Completed, null);

        Assert.Throws<InvalidStatusTransitionException>(
            () => appt.Transition(AppointmentStatus.Cancelled, null));
    }

    [Fact]
    public void Cancelled_cannot_transition_further()
    {
        var appt = CreateFutureAppointment();
        appt.Transition(AppointmentStatus.Cancelled, null);

        Assert.Throws<InvalidStatusTransitionException>(
            () => appt.Transition(AppointmentStatus.Confirmed, null));
    }

    [Fact]
    public void Transition_records_history_entry()
    {
        var appt = CreateFutureAppointment();
        appt.Transition(AppointmentStatus.Confirmed, "user-123", "Email confirmed");

        var entry = Assert.Single(appt.StatusHistory);
        Assert.Equal(AppointmentStatus.PendingConfirmation, entry.PreviousStatus);
        Assert.Equal(AppointmentStatus.Confirmed, entry.NewStatus);
        Assert.Equal("user-123", entry.ChangedByUserId);
        Assert.Equal("Email confirmed", entry.Reason);
    }

    [Fact]
    public void Cannot_create_appointment_in_the_past()
    {
        Assert.Throws<BusinessRuleViolationException>(() =>
            Appointment.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                startAtUtc: DateTime.UtcNow.AddMinutes(-5),
                durationMinutes: 30, bufferBeforeMinutes: 0, bufferAfterMinutes: 0,
                source: AppointmentSource.Public));
    }

    [Fact]
    public void Notes_exceeding_500_chars_are_rejected()
    {
        Assert.Throws<BusinessRuleViolationException>(() =>
            Appointment.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                startAtUtc: DateTime.UtcNow.AddHours(1),
                durationMinutes: 30, bufferBeforeMinutes: 0, bufferAfterMinutes: 0,
                source: AppointmentSource.Public,
                notes: new string('x', 501)));
    }

    [Fact]
    public void BlockedTimes_include_buffers()
    {
        var start = DateTime.UtcNow.AddHours(2);
        var appt = Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            startAtUtc: start,
            durationMinutes: 30,
            bufferBeforeMinutes: 15,
            bufferAfterMinutes: 10,
            source: AppointmentSource.Internal);

        Assert.Equal(start.AddMinutes(-15), appt.BlockedStartAtUtc);
        Assert.Equal(start.AddMinutes(30 + 10), appt.BlockedEndAtUtc);
        Assert.Equal(start, appt.StartAtUtc);
        Assert.Equal(start.AddMinutes(30), appt.EndAtUtc);
    }

    [Fact]
    public void Reschedule_resets_confirmed_to_pending()
    {
        var appt = CreateFutureAppointment();
        appt.Transition(AppointmentStatus.Confirmed, null);

        appt.Reschedule(DateTime.UtcNow.AddDays(1), 30, 0, 0, "user-1");

        Assert.Equal(AppointmentStatus.PendingConfirmation, appt.Status);
    }

    [Fact]
    public void Reschedule_fails_when_completed()
    {
        var appt = CreateFutureAppointment();
        appt.Transition(AppointmentStatus.Confirmed, null);
        appt.Transition(AppointmentStatus.CheckedIn, null);
        appt.Transition(AppointmentStatus.Completed, null);

        Assert.Throws<BusinessRuleViolationException>(() =>
            appt.Reschedule(DateTime.UtcNow.AddDays(1), 30, 0, 0, null));
    }
}

public class ServiceEntityTests
{
    [Fact]
    public void TotalBlockMinutes_includes_all_buffers()
    {
        var service = Service.Create(
            Guid.NewGuid(), "Corte", null,
            durationMinutes: 30, price: 50, currency: "BRL",
            bufferBeforeMinutes: 10, bufferAfterMinutes: 5);

        Assert.Equal(45, service.TotalBlockMinutes); // 10 + 30 + 5
    }

    [Fact]
    public void Cannot_create_service_with_zero_duration()
    {
        Assert.Throws<BusinessRuleViolationException>(() =>
            Service.Create(Guid.NewGuid(), "Test", null, 0, 50));
    }

    [Fact]
    public void Cannot_create_service_with_negative_price()
    {
        Assert.Throws<BusinessRuleViolationException>(() =>
            Service.Create(Guid.NewGuid(), "Test", null, 30, -1));
    }

    [Fact]
    public void Cannot_create_service_exceeding_8_hour_duration()
    {
        Assert.Throws<BusinessRuleViolationException>(() =>
            Service.Create(Guid.NewGuid(), "Test", null, 481, 0));
    }
}

public class BookingConfirmationTests
{
    [Fact]
    public void Valid_confirmation_can_be_used()
    {
        var confirmation = BookingConfirmation.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            "abc123hash",
            BookingConfirmationPurpose.ConfirmAppointment,
            DateTime.UtcNow.AddHours(48));

        Assert.True(confirmation.IsValid());
        confirmation.MarkAsUsed();
        Assert.NotNull(confirmation.UsedAtUtc);
    }

    [Fact]
    public void Expired_confirmation_is_invalid()
    {
        var confirmation = BookingConfirmation.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            "abc123hash",
            BookingConfirmationPurpose.ConfirmAppointment,
            DateTime.UtcNow.AddHours(-1)); // expired

        Assert.False(confirmation.IsValid());
    }

    [Fact]
    public void Cannot_use_expired_confirmation()
    {
        var confirmation = BookingConfirmation.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            "abc123hash",
            BookingConfirmationPurpose.ConfirmAppointment,
            DateTime.UtcNow.AddHours(-1));

        Assert.Throws<BusinessRuleViolationException>(() => confirmation.MarkAsUsed());
    }

    [Fact]
    public void Used_confirmation_is_no_longer_valid()
    {
        var confirmation = BookingConfirmation.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            "abc123hash",
            BookingConfirmationPurpose.ConfirmAppointment,
            DateTime.UtcNow.AddHours(48));

        confirmation.MarkAsUsed();
        Assert.False(confirmation.IsValid());
    }
}

public class OutboxMessageTests
{
    [Fact]
    public void Failed_message_uses_exponential_backoff()
    {
        var msg = OutboxMessage.Create("test.event", "{}");
        msg.MarkProcessing(); // attempt 1
        msg.MarkFailed("connection error");

        Assert.Equal(OutboxMessageStatus.Pending, msg.Status);
        Assert.True(msg.NextAttemptAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public void Message_becomes_dead_after_max_attempts()
    {
        var msg = OutboxMessage.Create("test.event", "{}");
        for (int i = 0; i < 5; i++)
        {
            msg.MarkProcessing();
            msg.MarkFailed("error", maxAttempts: 5);
        }

        Assert.Equal(OutboxMessageStatus.Dead, msg.Status);
    }

    [Fact]
    public void Sent_message_is_marked_correctly()
    {
        var msg = OutboxMessage.Create("test.event", "{}");
        msg.MarkProcessing();
        msg.MarkSent();

        Assert.Equal(OutboxMessageStatus.Sent, msg.Status);
        Assert.NotNull(msg.ProcessedAtUtc);
    }
}
