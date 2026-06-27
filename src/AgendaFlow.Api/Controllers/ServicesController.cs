using AgendaFlow.Application.Contracts;
using AgendaFlow.Application.DTOs;
using AgendaFlow.Domain.Entities;
using AgendaFlow.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendaFlow.Api.Controllers;

[ApiController]
[Route("api/services")]
[Authorize]
public sealed class ServicesController : ControllerBase
{
    private readonly IServiceRepository _services;
    private readonly IPlanLimitService _planLimits;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;

    public ServicesController(
        IServiceRepository services,
        IPlanLimitService planLimits,
        ITenantContext tenant,
        IUnitOfWork uow)
    {
        _services = services;
        _planLimits = planLimits;
        _tenant = tenant;
        _uow = uow;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? active, CancellationToken ct)
    {
        var list = await _services.ListAsync(_tenant.TenantId, active, ct);
        return Ok(list.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var service = await _services.FindByIdAsync(_tenant.TenantId, id, ct);
        // Return 404 for both "not found" and "belongs to another tenant" — no IDOR leakage
        if (service is null) return NotFound();
        return Ok(MapToResponse(service));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceRequest request, CancellationToken ct)
    {
        if (!await _planLimits.CanAddServiceAsync(_tenant.TenantId, ct))
            throw new PlanLimitExceededException("active services");

        var service = Service.Create(
            _tenant.TenantId,
            request.Name,
            request.Description,
            request.DurationMinutes,
            request.Price,
            request.Currency,
            request.BufferBeforeMinutes,
            request.BufferAfterMinutes);

        await _services.AddAsync(service, ct);
        await _uow.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = service.Id }, MapToResponse(service));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateServiceRequest request, CancellationToken ct)
    {
        var service = await _services.FindByIdAsync(_tenant.TenantId, id, ct);
        if (service is null) return NotFound();

        service.Update(request.Name, request.Description, request.DurationMinutes,
            request.Price, request.BufferBeforeMinutes, request.BufferAfterMinutes);
        await _uow.SaveChangesAsync(ct);

        return Ok(MapToResponse(service));
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var service = await _services.FindByIdAsync(_tenant.TenantId, id, ct);
        if (service is null) return NotFound();

        if (!await _planLimits.CanAddServiceAsync(_tenant.TenantId, ct))
            throw new PlanLimitExceededException("active services");

        service.Activate();
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var service = await _services.FindByIdAsync(_tenant.TenantId, id, ct);
        if (service is null) return NotFound();
        service.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    private static ServiceResponse MapToResponse(Service s) => new(
        s.Id, s.Name, s.Description, s.DurationMinutes, s.Price, s.Currency,
        s.BufferBeforeMinutes, s.BufferAfterMinutes, s.IsActive, s.CreatedAtUtc);
}
