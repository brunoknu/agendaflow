using AgendaFlow.Application.Contracts;
using AgendaFlow.Domain.Enums;
using AgendaFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgendaFlow.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public ReportsController(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>
    /// Returns appointment summary metrics for a date range.
    /// </summary>
    [HttpGet("appointments")]
    public async Task<IActionResult> AppointmentsSummary(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

        if (toDate < fromDate)
            return BadRequest(new { message = "End date must be after start date." });

        if ((toDate.DayNumber - fromDate.DayNumber) > 365)
            return BadRequest(new { message = "Date range cannot exceed 365 days." });

        var fromUtc = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = toDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var appointments = await _db.Appointments
            .Include(a => a.Professional)
            .Include(a => a.Service)
            .Where(a => a.StartAtUtc >= fromUtc && a.StartAtUtc <= toUtc)
            .ToListAsync(ct);

        var byStatus = appointments
            .GroupBy(a => a.Status.ToString())
            .Select(g => new StatusCount(g.Key, g.Count()))
            .OrderByDescending(s => s.Count)
            .ToList();

        var byProfessional = appointments
            .Where(a => a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
            .GroupBy(a => a.Professional?.Name ?? "Unknown")
            .Select(g => new ProfessionalStats(g.Key, g.Count()))
            .OrderByDescending(p => p.Count)
            .ToList();

        var byService = appointments
            .Where(a => a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.NoShow)
            .GroupBy(a => a.Service?.Name ?? "Unknown")
            .Select(g => new ServiceStats(g.Key, g.Count()))
            .OrderByDescending(s => s.Count)
            .ToList();

        var total = appointments.Count;
        var completed = appointments.Count(a => a.Status == AppointmentStatus.Completed);
        var cancelled = appointments.Count(a => a.Status == AppointmentStatus.Cancelled);
        var noShow = appointments.Count(a => a.Status == AppointmentStatus.NoShow);

        var cancellationRate = total > 0
            ? Math.Round((double)(cancelled + noShow) / total * 100, 1)
            : 0.0;

        // Daily breakdown
        var byDay = appointments
            .GroupBy(a => DateOnly.FromDateTime(a.StartAtUtc))
            .Select(g => new DayCount(g.Key.ToString("yyyy-MM-dd"), g.Count()))
            .OrderBy(d => d.Date)
            .ToList();

        return Ok(new
        {
            from = fromDate.ToString("yyyy-MM-dd"),
            to = toDate.ToString("yyyy-MM-dd"),
            total,
            completed,
            cancelled,
            noShow,
            cancellationRate,
            byStatus,
            byProfessional,
            byService,
            byDay,
        });
    }
}

internal record StatusCount(string Status, int Count);
internal record ProfessionalStats(string ProfessionalName, int Count);
internal record ServiceStats(string ServiceName, int Count);
internal record DayCount(string Date, int Count);
