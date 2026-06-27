using AgendaFlow.Domain.Enums;

namespace AgendaFlow.Application.DTOs;

// ── Auth ──────────────────────────────────────────────────────────

public record RegisterRequest(
    string Email,
    string Password,
    string FullName);

public record LoginRequest(
    string Email,
    string Password);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword);

public record ConfirmEmailRequest(
    string UserId,
    string Token);

// ── Tenant / Onboarding ──────────────────────────────────────────

public record CreateTenantRequest(
    string Name,
    string Slug,
    string TimeZoneId);

public record UpdateTenantRequest(
    string Name,
    string TimeZoneId);

public record TenantResponse(
    Guid Id,
    string Name,
    string Slug,
    string TimeZoneId,
    string Plan,
    string Status,
    DateTime CreatedAtUtc);

// ── Service ──────────────────────────────────────────────────────

public record CreateServiceRequest(
    string Name,
    string? Description,
    int DurationMinutes,
    decimal Price,
    string Currency = "BRL",
    int BufferBeforeMinutes = 0,
    int BufferAfterMinutes = 0);

public record UpdateServiceRequest(
    string Name,
    string? Description,
    int DurationMinutes,
    decimal Price,
    int BufferBeforeMinutes,
    int BufferAfterMinutes);

public record ServiceResponse(
    Guid Id,
    string Name,
    string? Description,
    int DurationMinutes,
    decimal Price,
    string Currency,
    int BufferBeforeMinutes,
    int BufferAfterMinutes,
    bool IsActive,
    DateTime CreatedAtUtc);

// ── Professional ─────────────────────────────────────────────────

public record CreateProfessionalRequest(
    string Name,
    string? Email,
    string? Phone);

public record UpdateProfessionalRequest(
    string Name,
    string? Email,
    string? Phone);

public record ProfessionalResponse(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    bool IsActive,
    DateTime CreatedAtUtc,
    IReadOnlyList<Guid>? ServiceIds = null);

// ── Availability ─────────────────────────────────────────────────

public record CreateAvailabilityRuleRequest(
    DayOfWeek DayOfWeek,
    TimeOnly StartLocalTime,
    TimeOnly EndLocalTime,
    int SlotIntervalMinutes = 30);

public record AddAvailabilityExceptionRequest(
    DateOnly Date,
    string Type, // "DayOff" | "SpecialHours"
    TimeOnly? StartLocalTime,
    TimeOnly? EndLocalTime,
    string? Reason);

public record AvailabilityRuleResponse(
    Guid Id,
    DayOfWeek DayOfWeek,
    TimeOnly StartLocalTime,
    TimeOnly EndLocalTime,
    int SlotIntervalMinutes,
    bool IsActive);

public record AvailableSlotsRequest(
    Guid ProfessionalId,
    Guid ServiceId,
    DateOnly Date);

public record AvailableSlotsResponse(IReadOnlyList<DateTime> Slots);

// ── Appointment ──────────────────────────────────────────────────

public record CreateAppointmentRequest(
    Guid ProfessionalId,
    Guid ServiceId,
    Guid CustomerId,
    DateTime StartAtUtc,
    string? Notes);

public record PublicBookingRequest(
    Guid ProfessionalId,
    Guid ServiceId,
    DateTime StartAtUtc,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    string? Notes,
    string IdempotencyKey);

public record AppointmentResponse(
    Guid Id,
    Guid ProfessionalId,
    string ProfessionalName,
    Guid ServiceId,
    string ServiceName,
    Guid CustomerId,
    string CustomerName,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string Status,
    string Source,
    string? Notes,
    DateTime CreatedAtUtc);

public record AppointmentDetailResponse(
    Guid Id,
    ProfessionalResponse Professional,
    ServiceResponse Service,
    CustomerResponse Customer,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string Status,
    string Source,
    string? Notes,
    IReadOnlyList<StatusHistoryEntry> StatusHistory,
    DateTime CreatedAtUtc);

public record StatusHistoryEntry(
    string PreviousStatus,
    string NewStatus,
    string? ChangedByUserId,
    DateTime ChangedAtUtc,
    string? Reason);

public record UpdateAppointmentStatusRequest(
    string NewStatus,
    string? Reason);

// ── Customer ─────────────────────────────────────────────────────

public record CustomerResponse(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    DateTime CreatedAtUtc);

// ── Dashboard ────────────────────────────────────────────────────

public record DashboardResponse(
    int TodayAppointments,
    int UpcomingAppointments,
    IReadOnlyList<StatusCount> AppointmentsByStatus,
    IReadOnlyList<ServiceStats> TopServices,
    decimal CancellationRate,
    IReadOnlyList<ProfessionalStats> TopProfessionals);

public record StatusCount(string Status, int Count);
public record ServiceStats(string ServiceName, int Count);
public record ProfessionalStats(string ProfessionalName, int Count);

// ── Pagination ───────────────────────────────────────────────────

public record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
