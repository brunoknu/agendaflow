using System.Security.Cryptography;
using AgendaFlow.Application.Contracts;
using AgendaFlow.Application.DTOs;
using AgendaFlow.Domain.Entities;
using AgendaFlow.Domain.Enums;
using AgendaFlow.Domain.Exceptions;

namespace AgendaFlow.Application.Appointments;

public sealed class AppointmentService
{
    private readonly IAppointmentRepository _appointments;
    private readonly IServiceRepository _services;
    private readonly IProfessionalRepository _professionals;
    private readonly ICustomerRepository _customers;
    private readonly IAvailabilityRepository _availability;
    private readonly IBookingConfirmationRepository _confirmations;
    private readonly IOutboxRepository _outbox;
    private readonly IPlanLimitService _planLimits;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public AppointmentService(
        IAppointmentRepository appointments,
        IServiceRepository services,
        IProfessionalRepository professionals,
        ICustomerRepository customers,
        IAvailabilityRepository availability,
        IBookingConfirmationRepository confirmations,
        IOutboxRepository outbox,
        IPlanLimitService planLimits,
        IUnitOfWork uow,
        IClock clock)
    {
        _appointments = appointments;
        _services = services;
        _professionals = professionals;
        _customers = customers;
        _availability = availability;
        _confirmations = confirmations;
        _outbox = outbox;
        _planLimits = planLimits;
        _uow = uow;
        _clock = clock;
    }

    /// <summary>
    /// Books an appointment via the public page (no authenticated user required).
    /// Idempotent: same key + same payload returns the existing appointment.
    /// </summary>
    public async Task<AppointmentResponse> BookPublicAsync(
        Guid tenantId,
        PublicBookingRequest request,
        CancellationToken ct)
    {
        // Idempotency check
        var existing = await _appointments.FindByIdempotencyKeyAsync(request.IdempotencyKey, ct);
        if (existing is not null)
            return MapToResponse(existing);

        // Plan limit
        if (!await _planLimits.CanCreateAppointmentAsync(tenantId, ct))
            throw new PlanLimitExceededException("appointments");

        var service = await _services.FindByIdAsync(tenantId, request.ServiceId, ct)
            ?? throw new BusinessRuleViolationException("Service not found.");
        if (!service.IsActive)
            throw new BusinessRuleViolationException("The selected service is not available.");

        var professional = await _professionals.FindByIdAsync(tenantId, request.ProfessionalId, ct)
            ?? throw new BusinessRuleViolationException("Professional not found.");
        if (!professional.IsActive)
            throw new BusinessRuleViolationException("The selected professional is not available.");
        if (!professional.OffersService(request.ServiceId))
            throw new BusinessRuleViolationException("This professional does not offer the selected service.");

        if (request.StartAtUtc <= _clock.UtcNow)
            throw new BusinessRuleViolationException("Cannot book an appointment in the past.");

        // Find or create customer
        var customer = await _customers.FindByEmailAsync(tenantId, request.CustomerEmail, ct);
        if (customer is null)
        {
            customer = Customer.Create(tenantId, request.CustomerName, request.CustomerEmail, request.CustomerPhone);
            await _customers.AddAsync(customer, ct);
        }

        // Create appointment in transaction — the DB exclusion constraint is the final concurrency guard
        Appointment appointment = null!;
        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            appointment = Appointment.Create(
                tenantId,
                professional.Id,
                service.Id,
                customer.Id,
                request.StartAtUtc,
                service.DurationMinutes,
                service.BufferBeforeMinutes,
                service.BufferAfterMinutes,
                AppointmentSource.Public,
                request.Notes);

            await _appointments.AddAsync(appointment, innerCt);

            // Queue confirmation email via outbox (guaranteed delivery)
            var emailPayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                AppointmentId = appointment.Id,
                CustomerEmail = request.CustomerEmail,
                CustomerName = request.CustomerName,
                ServiceName = service.Name,
                ProfessionalName = professional.Name,
                StartAtUtc = request.StartAtUtc,
                TenantId = tenantId
            });

            var outboxMsg = OutboxMessage.Create("booking.created", emailPayload);
            await _outbox.AddAsync(outboxMsg, innerCt);

            await _uow.SaveChangesAsync(innerCt);
        }, ct);

        // Create email confirmation token
        var token = GenerateSecureToken();
        var tokenHash = HashToken(token);
        var confirmation = BookingConfirmation.Create(
            tenantId,
            appointment.Id,
            tokenHash,
            BookingConfirmationPurpose.ConfirmAppointment,
            _clock.UtcNow.AddHours(48));
        await _confirmations.AddAsync(confirmation, ct);
        await _uow.SaveChangesAsync(ct);

        return MapToResponse(appointment);
    }

    public async Task ConfirmByTokenAsync(string rawToken, CancellationToken ct)
    {
        var tokenHash = HashToken(rawToken);
        var confirmation = await _confirmations.FindByTokenHashAsync(tokenHash, ct)
            ?? throw new BusinessRuleViolationException("Confirmation link is invalid or has already been used.");

        if (!confirmation.IsValid())
            throw new BusinessRuleViolationException("This confirmation link has expired.");

        var appointment = await _appointments.FindByIdAsync(confirmation.TenantId, confirmation.AppointmentId, ct)
            ?? throw new BusinessRuleViolationException("Appointment not found.");

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            appointment.Transition(AppointmentStatus.Confirmed, null, "Confirmed via email link");
            confirmation.MarkAsUsed();

            var emailPayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                appointment.Id,
                appointment.StartAtUtc,
                appointment.TenantId
            });
            await _outbox.AddAsync(OutboxMessage.Create("booking.confirmed", emailPayload), innerCt);
            await _uow.SaveChangesAsync(innerCt);
        }, ct);
    }

    public async Task CancelByTokenAsync(string rawToken, CancellationToken ct)
    {
        var tokenHash = HashToken(rawToken);
        var confirmation = await _confirmations.FindByTokenHashAsync(tokenHash, ct)
            ?? throw new BusinessRuleViolationException("Cancellation link is invalid.");

        if (!confirmation.IsValid())
            throw new BusinessRuleViolationException("This cancellation link has expired.");

        if (confirmation.Purpose != BookingConfirmationPurpose.CancelAppointment)
            throw new BusinessRuleViolationException("Invalid link type.");

        var appointment = await _appointments.FindByIdAsync(confirmation.TenantId, confirmation.AppointmentId, ct)
            ?? throw new BusinessRuleViolationException("Appointment not found.");

        await _uow.ExecuteInTransactionAsync(async innerCt =>
        {
            appointment.Transition(AppointmentStatus.Cancelled, null, "Cancelled by customer via link");
            confirmation.MarkAsUsed();

            var emailPayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                appointment.Id,
                appointment.TenantId,
                Reason = "Customer cancelled"
            });
            await _outbox.AddAsync(OutboxMessage.Create("booking.cancelled", emailPayload), innerCt);
            await _uow.SaveChangesAsync(innerCt);
        }, ct);
    }

    public async Task UpdateStatusAsync(
        Guid tenantId,
        Guid appointmentId,
        AppointmentStatus newStatus,
        string? changedByUserId,
        string? reason,
        CancellationToken ct)
    {
        var appointment = await _appointments.FindByIdAsync(tenantId, appointmentId, ct)
            ?? throw new BusinessRuleViolationException("Appointment not found.");

        appointment.Transition(newStatus, changedByUserId, reason);
        await _uow.SaveChangesAsync(ct);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static AppointmentResponse MapToResponse(Appointment a)
        => new(
            a.Id,
            a.ProfessionalId,
            a.Professional?.Name ?? string.Empty,
            a.ServiceId,
            a.Service?.Name ?? string.Empty,
            a.CustomerId,
            a.Customer?.Name ?? string.Empty,
            a.StartAtUtc,
            a.EndAtUtc,
            a.Status.ToString(),
            a.Source.ToString(),
            a.Notes,
            a.CreatedAtUtc);
}
