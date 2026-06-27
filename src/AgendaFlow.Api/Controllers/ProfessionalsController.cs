using AgendaFlow.Application.Contracts;
using AgendaFlow.Application.DTOs;
using AgendaFlow.Domain.Entities;
using AgendaFlow.Domain.Exceptions;
using AgendaFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgendaFlow.Api.Controllers;

[ApiController]
[Route("api/professionals")]
[Authorize]
public sealed class ProfessionalsController : ControllerBase
{
    private readonly IProfessionalRepository _professionals;
    private readonly IServiceRepository _services;
    private readonly IPlanLimitService _planLimits;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _db;

    public ProfessionalsController(
        IProfessionalRepository professionals,
        IServiceRepository services,
        IPlanLimitService planLimits,
        ITenantContext tenant,
        IUnitOfWork uow,
        AppDbContext db)
    {
        _professionals = professionals;
        _services = services;
        _planLimits = planLimits;
        _tenant = tenant;
        _uow = uow;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? active, CancellationToken ct)
    {
        var list = await _professionals.ListAsync(_tenant.TenantId, active, ct);
        return Ok(list.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var professional = await _professionals.FindByIdAsync(_tenant.TenantId, id, ct);
        if (professional is null) return NotFound();
        return Ok(MapToResponse(professional));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProfessionalRequest request, CancellationToken ct)
    {
        if (!await _planLimits.CanAddProfessionalAsync(_tenant.TenantId, ct))
            throw new PlanLimitExceededException("active professionals");

        var professional = Professional.Create(_tenant.TenantId, request.Name, request.Email, request.Phone);
        await _professionals.AddAsync(professional, ct);
        await _uow.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = professional.Id }, MapToResponse(professional));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProfessionalRequest request, CancellationToken ct)
    {
        var professional = await _professionals.FindByIdAsync(_tenant.TenantId, id, ct);
        if (professional is null) return NotFound();

        professional.Update(request.Name, request.Email, request.Phone);
        await _uow.SaveChangesAsync(ct);

        return Ok(MapToResponse(professional));
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var professional = await _professionals.FindByIdAsync(_tenant.TenantId, id, ct);
        if (professional is null) return NotFound();

        if (!await _planLimits.CanAddProfessionalAsync(_tenant.TenantId, ct))
            throw new PlanLimitExceededException("active professionals");

        professional.Activate();
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var professional = await _professionals.FindByIdAsync(_tenant.TenantId, id, ct);
        if (professional is null) return NotFound();
        professional.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/services/{serviceId:guid}")]
    public async Task<IActionResult> LinkService(Guid id, Guid serviceId, CancellationToken ct)
    {
        var professional = await _professionals.FindByIdAsync(_tenant.TenantId, id, ct);
        if (professional is null) return NotFound();

        var service = await _services.FindByIdAsync(_tenant.TenantId, serviceId, ct);
        if (service is null) return NotFound();

        var alreadyLinked = await _db.ProfessionalServices
            .AnyAsync(ps => ps.ProfessionalId == id && ps.ServiceId == serviceId, ct);
        if (alreadyLinked) return Conflict(new { message = "Service is already linked to this professional." });

        await _db.ProfessionalServices.AddAsync(ProfessionalService.Create(id, serviceId), ct);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/services/{serviceId:guid}")]
    public async Task<IActionResult> UnlinkService(Guid id, Guid serviceId, CancellationToken ct)
    {
        var link = await _db.ProfessionalServices
            .FirstOrDefaultAsync(ps => ps.ProfessionalId == id && ps.ServiceId == serviceId, ct);
        if (link is null) return NotFound();

        _db.ProfessionalServices.Remove(link);
        await _uow.SaveChangesAsync(ct);
        return NoContent();
    }

    private static ProfessionalResponse MapToResponse(Professional p) =>
        new(p.Id, p.Name, p.Email, p.Phone, p.IsActive, p.CreatedAtUtc,
            p.ProfessionalServices.Select(ps => ps.ServiceId).ToList());
}
