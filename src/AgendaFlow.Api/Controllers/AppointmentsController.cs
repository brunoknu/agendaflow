using AgendaFlow.Application.Contracts;
using AgendaFlow.Application.DTOs;
using AgendaFlow.Domain.Enums;
using AgendaFlow.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AgendaFlow.Infrastructure.Identity;

namespace AgendaFlow.Api.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize]
public sealed class AppointmentsController : ControllerBase
{
    private readonly IAppointmentRepository _appointments;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly UserManager<ApplicationUser> _userManager;

    public AppointmentsController(
        IAppointmentRepository appointments,
        ITenantContext tenant,
        IUnitOfWork uow,
        UserManager<ApplicationUser> userManager)
    {
        _appointments = appointments;
        _tenant = tenant;
        _uow = uow;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? professionalId,
        [FromQuery] Guid? serviceId,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        AppointmentStatus? parsedStatus = null;
        if (status is not null && Enum.TryParse<AppointmentStatus>(status, ignoreCase: true, out var s))
            parsedStatus = s;

        var filter = new AppointmentFilter(
            _tenant.TenantId, professionalId, serviceId, parsedStatus,
            from, to, page, Math.Clamp(pageSize, 1, 100));

        var result = await _appointments.ListAsync(filter, ct);

        return Ok(new PaginatedResponse<AppointmentResponse>(
            result.Items.Select(MapToResponse).ToList(),
            result.TotalCount,
            result.Page,
            result.PageSize,
            result.TotalPages));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var appointment = await _appointments.FindByIdAsync(_tenant.TenantId, id, ct);
        if (appointment is null) return NotFound();
        return Ok(MapToResponse(appointment));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateAppointmentStatusRequest request,
        CancellationToken ct)
    {
        var appointment = await _appointments.FindByIdAsync(_tenant.TenantId, id, ct);
        if (appointment is null) return NotFound();

        if (!Enum.TryParse<AppointmentStatus>(request.NewStatus, ignoreCase: true, out var newStatus))
            return BadRequest(new { message = $"Invalid status: {request.NewStatus}" });

        var userId = _userManager.GetUserId(User);
        appointment.Transition(newStatus, userId, request.Reason);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    private static AppointmentResponse MapToResponse(Domain.Entities.Appointment a) =>
        new(a.Id,
            a.ProfessionalId, a.Professional?.Name ?? string.Empty,
            a.ServiceId, a.Service?.Name ?? string.Empty,
            a.CustomerId, a.Customer?.Name ?? string.Empty,
            a.StartAtUtc, a.EndAtUtc,
            a.Status.ToString(), a.Source.ToString(),
            a.Notes, a.CreatedAtUtc);
}
