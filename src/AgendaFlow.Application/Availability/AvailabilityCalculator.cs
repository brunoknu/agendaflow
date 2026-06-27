using AgendaFlow.Domain.Entities;

namespace AgendaFlow.Application.Availability;

/// <summary>
/// Computes available time slots for a professional on a given date,
/// respecting recurring rules, exceptions, and existing appointments.
/// All times are in the tenant's local timezone but stored/compared in UTC.
/// </summary>
public sealed class AvailabilityCalculator
{
    /// <summary>
    /// Returns UTC start times where the professional is free
    /// to begin a service of the given duration.
    /// </summary>
    public static IReadOnlyList<DateTime> ComputeAvailableSlots(
        DateOnly localDate,
        TimeZoneInfo timeZone,
        IReadOnlyList<AvailabilityRule> rules,
        IReadOnlyList<AvailabilityException> exceptions,
        IReadOnlyList<(DateTime BlockedStart, DateTime BlockedEnd)> existingBlocks,
        int durationMinutes,
        int bufferBefore,
        int bufferAfter,
        DateTime minimumStartUtc)
    {
        // Check if there's a full-day exception for this date
        var dayException = exceptions.FirstOrDefault(e => e.Date == localDate);
        if (dayException is not null && dayException.IsFullDayOff)
            return [];

        var dayOfWeek = localDate.DayOfWeek;
        var activeRules = rules
            .Where(r => r.IsActive && r.DayOfWeek == dayOfWeek)
            .ToList();

        // If there's a special hours exception, it overrides the recurring rules
        TimeOnly? overrideStart = null;
        TimeOnly? overrideEnd = null;
        if (dayException is { Type: Domain.Enums.AvailabilityExceptionType.SpecialHours }
            && dayException.StartLocalTime.HasValue && dayException.EndLocalTime.HasValue)
        {
            overrideStart = dayException.StartLocalTime;
            overrideEnd = dayException.EndLocalTime;
        }

        var slots = new List<DateTime>();
        var totalBlock = bufferBefore + durationMinutes + bufferAfter;

        foreach (var rule in activeRules)
        {
            var windowStart = overrideStart ?? rule.StartLocalTime;
            var windowEnd = overrideEnd ?? rule.EndLocalTime;

            var current = windowStart;
            while (current.AddMinutes(totalBlock) <= windowEnd)
            {
                // Convert local time to UTC
                var localDt = localDate.ToDateTime(current);
                DateTime utcStart;
                try
                {
                    utcStart = TimeZoneInfo.ConvertTimeToUtc(localDt, timeZone);
                }
                catch (ArgumentException)
                {
                    // Skip invalid/ambiguous times (DST transitions)
                    current = current.AddMinutes(rule.SlotIntervalMinutes);
                    continue;
                }

                var blockedStart = utcStart.AddMinutes(-bufferBefore);
                var blockedEnd = utcStart.AddMinutes(durationMinutes + bufferAfter);

                if (utcStart >= minimumStartUtc && !OverlapsExistingBlock(blockedStart, blockedEnd, existingBlocks))
                {
                    slots.Add(utcStart);
                }

                current = current.AddMinutes(rule.SlotIntervalMinutes);
            }
        }

        return slots.Distinct().OrderBy(s => s).ToList();
    }

    private static bool OverlapsExistingBlock(
        DateTime start,
        DateTime end,
        IReadOnlyList<(DateTime BlockedStart, DateTime BlockedEnd)> blocks)
    {
        // Using half-open intervals [start, end) so adjacent slots don't conflict
        return blocks.Any(b => start < b.BlockedEnd && end > b.BlockedStart);
    }
}
