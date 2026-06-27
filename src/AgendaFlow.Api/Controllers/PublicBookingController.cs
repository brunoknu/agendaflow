using AgendaFlow.Application.Appointments;
using AgendaFlow.Application.Availability;
using AgendaFlow.Application.Contracts;
using AgendaFlow.Application.DTOs;
using AgendaFlow.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace AgendaFlow.Api.Controllers;

/// <summary>
/// Public endpoints — no authentication required.
/// Rate limiting is applied per IP and per tenant.
/// Tenant is resolved by slug from the URL, never from request body.
/// </summary>
[ApiController]
[Route("api/public/{tenantSlug}")]
public sealed class PublicBookingController : ControllerBase
{
    private readonly ITenantRepository _tenants;
    private readonly IServiceRepository _services;
    private readonly IProfessionalRepository _professionals;
    private readonly IAvailabilityRepository _availability;
    private readonly IAppointmentRepository _appointments;
    private readonly AppointmentService _appointmentService;
    private readonly IClock _clock;

    public PublicBookingController(
        ITenantRepository tenants,
        IServiceRepository services,
        IProfessionalRepository professionals,
        IAvailabilityRepository availability,
        IAppointmentRepository appointments,
        AppointmentService appointmentService,
        IClock clock)
    {
        _tenants = tenants;
        _services = services;
        _professionals = professionals;
        _availability = availability;
        _appointments = appointments;
        _appointmentService = appointmentService;
        _clock = clock;
    }

    [HttpGet("services")]
    public async Task<IActionResult> GetServices(string tenantSlug, CancellationToken ct)
    {
        var tenant = await ResolveTenantAsync(tenantSlug, ct);
        var list = await _services.ListAsync(tenant.Id, activeOnly: true, ct);
        return Ok(list.Select(s => new { s.Id, s.Name, s.Description, s.DurationMinutes, s.Price, s.Currency }));
    }

    [HttpGet("professionals")]
    public async Task<IActionResult> GetProfessionals(string tenantSlug, [FromQuery] Guid? serviceId, CancellationToken ct)
    {
        var tenant = await ResolveTenantAsync(tenantSlug, ct);
        var all = await _professionals.ListAsync(tenant.Id, activeOnly: true, ct);

        var result = serviceId.HasValue
            ? all.Where(p => p.OffersService(serviceId.Value))
            : all;

        // Expose only minimal info on public page — no email/phone
        return Ok(result.Select(p => new { p.Id, p.Name }));
    }

    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailableSlots(
        string tenantSlug,
        [FromQuery] Guid professionalId,
        [FromQuery] Guid serviceId,
        [FromQuery] DateOnly date,
        CancellationToken ct)
    {
        var tenant = await ResolveTenantAsync(tenantSlug, ct);

        var service = await _services.FindByIdAsync(tenant.Id, serviceId, ct);
        if (service is null || !service.IsActive) return NotFound();

        var professional = await _professionals.FindByIdAsync(tenant.Id, professionalId, ct);
        if (professional is null || !professional.IsActive) return NotFound();
        if (!professional.OffersService(serviceId)) return NotFound();

        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZoneId); }
        catch { tz = TimeZoneInfo.Utc; }

        var rules = await _availability.GetRulesAsync(tenant.Id, professionalId, ct);
        var exceptions = await _availability.GetExceptionsAsync(tenant.Id, professionalId, date, date, ct);

        // Get existing appointments for the day
        var dayStart = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);
        var filter = new AppointmentFilter(tenant.Id, professionalId,
            From: dayStart, To: dayEnd, PageSize: 100);
        var existing = await _appointments.ListAsync(filter, ct);
        var blocks = existing.Items
            .Where(a => a.Status is Domain.Enums.AppointmentStatus.PendingConfirmation
                     or Domain.Enums.AppointmentStatus.Confirmed
                     or Domain.Enums.AppointmentStatus.CheckedIn)
            .Select(a => (a.BlockedStartAtUtc, a.BlockedEndAtUtc))
            .ToList();

        var minStart = _clock.UtcNow.AddMinutes(30); // require 30-minute advance notice
        var slots = AvailabilityCalculator.ComputeAvailableSlots(
            date, tz, rules, exceptions, blocks,
            service.DurationMinutes, service.BufferBeforeMinutes, service.BufferAfterMinutes,
            minStart);

        return Ok(new { slots });
    }

    [HttpPost("appointments")]
    public async Task<IActionResult> Book(
        string tenantSlug,
        [FromBody] PublicBookingRequest request,
        CancellationToken ct)
    {
        var tenant = await ResolveTenantAsync(tenantSlug, ct);
        var result = await _appointmentService.BookPublicAsync(tenant.Id, request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    private async Task<Domain.Entities.Tenant> ResolveTenantAsync(string slug, CancellationToken ct)
    {
        var tenant = await _tenants.FindBySlugAsync(slug, ct);
        if (tenant is null || !tenant.IsActive())
            throw new TenantNotFoundException(slug);
        return tenant;
    }
}

[ApiController]
[Route("api/public/bookings")]
public sealed class BookingConfirmationController : ControllerBase
{
    private readonly AppointmentService _appointmentService;

    public BookingConfirmationController(AppointmentService appointmentService)
        => _appointmentService = appointmentService;

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromQuery] string token, CancellationToken ct)
    {
        await _appointmentService.ConfirmByTokenAsync(token, ct);
        return Ok(new { message = "Agendamento confirmado." });
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromQuery] string token, CancellationToken ct)
    {
        await _appointmentService.CancelByTokenAsync(token, ct);
        return Ok(new { message = "Agendamento cancelado." });
    }
}
