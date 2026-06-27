using AgendaFlow.Application.Availability;
using AgendaFlow.Domain.Entities;
using AgendaFlow.Domain.Enums;
using Xunit;

namespace AgendaFlow.UnitTests.Availability;

public class AvailabilityCalculatorTests
{
    private static readonly TimeZoneInfo SaoPaulo =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private static AvailabilityRule MakeRule(DayOfWeek day, TimeOnly start, TimeOnly end, int interval = 30)
    {
        return AvailabilityRule.Create(Guid.NewGuid(), Guid.NewGuid(), day, start, end, interval);
    }

    [Fact]
    public void Returns_empty_when_professional_has_no_rules()
    {
        var date = new DateOnly(2025, 6, 16); // Monday
        var slots = AvailabilityCalculator.ComputeAvailableSlots(
            date, SaoPaulo, [], [], [],
            durationMinutes: 30, bufferBefore: 0, bufferAfter: 0,
            minimumStartUtc: DateTime.UtcNow.AddDays(-1));

        Assert.Empty(slots);
    }

    [Fact]
    public void Returns_empty_when_day_has_no_matching_rule()
    {
        var date = new DateOnly(2025, 6, 16); // Monday
        var rule = MakeRule(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 0));

        var slots = AvailabilityCalculator.ComputeAvailableSlots(
            date, SaoPaulo, [rule], [], [],
            durationMinutes: 30, bufferBefore: 0, bufferAfter: 0,
            minimumStartUtc: DateTime.UtcNow.AddDays(-1));

        Assert.Empty(slots);
    }

    [Fact]
    public void Returns_correct_slots_for_two_hour_window()
    {
        var date = new DateOnly(2025, 6, 16); // Monday
        var rule = MakeRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0), interval: 60);

        var slots = AvailabilityCalculator.ComputeAvailableSlots(
            date, SaoPaulo, [rule], [], [],
            durationMinutes: 60, bufferBefore: 0, bufferAfter: 0,
            minimumStartUtc: DateTime.UtcNow.AddDays(-1));

        // 9:00 and 10:00 — last slot (11:00) can't fit a 60-min service
        Assert.Equal(2, slots.Count);
    }

    [Fact]
    public void Buffers_reduce_available_slots()
    {
        var date = new DateOnly(2025, 6, 16); // Monday
        // 9:00–11:00 window, 30-min interval, service=30min but buffer=15+15
        // Total block = 60 min, so only 09:00 and 10:00 fit
        var rule = MakeRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0), interval: 30);

        var slots = AvailabilityCalculator.ComputeAvailableSlots(
            date, SaoPaulo, [rule], [], [],
            durationMinutes: 30, bufferBefore: 15, bufferAfter: 15,
            minimumStartUtc: DateTime.UtcNow.AddDays(-1));

        Assert.Equal(2, slots.Count);
    }

    [Fact]
    public void Full_day_exception_returns_empty_slots()
    {
        var date = new DateOnly(2025, 6, 16); // Monday
        var rule = MakeRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        var dayOff = AvailabilityException.CreateDayOff(Guid.NewGuid(), Guid.NewGuid(), date);

        var slots = AvailabilityCalculator.ComputeAvailableSlots(
            date, SaoPaulo, [rule], [dayOff], [],
            durationMinutes: 30, bufferBefore: 0, bufferAfter: 0,
            minimumStartUtc: DateTime.UtcNow.AddDays(-1));

        Assert.Empty(slots);
    }

    [Fact]
    public void Special_hours_exception_overrides_rule()
    {
        var date = new DateOnly(2025, 6, 16); // Monday
        var rule = MakeRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0), interval: 60);
        // Special hours: only 10:00–12:00
        var special = AvailabilityException.CreateSpecialHours(
            Guid.NewGuid(), Guid.NewGuid(), date,
            new TimeOnly(10, 0), new TimeOnly(12, 0));

        var slots = AvailabilityCalculator.ComputeAvailableSlots(
            date, SaoPaulo, [rule], [special], [],
            durationMinutes: 60, bufferBefore: 0, bufferAfter: 0,
            minimumStartUtc: DateTime.UtcNow.AddDays(-1));

        Assert.Equal(2, slots.Count); // 10:00 and 11:00
    }

    [Fact]
    public void Existing_appointment_block_removes_overlapping_slots()
    {
        var date = new DateOnly(2025, 6, 16); // Monday
        var rule = MakeRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), interval: 60);

        // Block the 10:00 slot (UTC-3 → 13:00 UTC)
        var tz = SaoPaulo;
        var blockStart = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(date.Year, date.Month, date.Day, 10, 0, 0), tz);
        var blockEnd = blockStart.AddMinutes(60);

        var slots = AvailabilityCalculator.ComputeAvailableSlots(
            date, SaoPaulo, [rule], [],
            [(blockStart, blockEnd)],
            durationMinutes: 60, bufferBefore: 0, bufferAfter: 0,
            minimumStartUtc: DateTime.UtcNow.AddDays(-1));

        // 9:00 available, 10:00 blocked, 11:00 available
        Assert.Equal(2, slots.Count);
        Assert.DoesNotContain(blockStart, slots);
    }

    [Fact]
    public void Minimum_start_time_filters_past_slots()
    {
        var date = new DateOnly(2025, 6, 16); // Monday
        var rule = MakeRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0), interval: 60);

        // Set minimum to 12:00 UTC (09:00 São Paulo = 12:00 UTC)
        var tz = SaoPaulo;
        var minStart = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(date.Year, date.Month, date.Day, 13, 0, 0), tz); // 10:00 local

        var slots = AvailabilityCalculator.ComputeAvailableSlots(
            date, SaoPaulo, [rule], [], [],
            durationMinutes: 60, bufferBefore: 0, bufferAfter: 0,
            minimumStartUtc: minStart);

        // Only slots at 13:00, 14:00, 15:00, 16:00 local (≥ 10:00 minimum)
        Assert.All(slots, s => Assert.True(s >= minStart));
    }

    [Fact]
    public void Adjacent_slots_do_not_conflict_with_each_other()
    {
        // Regression: half-open intervals [start, end) allow back-to-back appointments
        var date = new DateOnly(2025, 6, 16);
        var tz = SaoPaulo;

        var slotEnd = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(date.Year, date.Month, date.Day, 10, 0, 0), tz);
        var slotStart = slotEnd; // next appointment starts exactly when previous ends

        var existingBlocks = new List<(DateTime, DateTime)>
        {
            (slotEnd.AddMinutes(-30), slotEnd) // previous appointment ended at 10:00
        };

        var rule = MakeRule(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), interval: 30);

        var slots = AvailabilityCalculator.ComputeAvailableSlots(
            date, SaoPaulo, [rule], [], existingBlocks,
            durationMinutes: 30, bufferBefore: 0, bufferAfter: 0,
            minimumStartUtc: DateTime.UtcNow.AddDays(-1));

        // 10:00 slot should be available despite the adjacent block
        Assert.Contains(slotStart, slots);
    }
}
