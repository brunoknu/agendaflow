using AgendaFlow.Application.Contracts;
using AgendaFlow.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgendaFlow.Infrastructure.Outbox;

/// <summary>
/// Background service that polls the outbox table and sends queued emails.
/// Runs every 30 seconds and processes up to 20 messages per batch.
/// Implements idempotency: if an email was already sent, marking it again is safe.
/// </summary>
public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;

    public OutboxProcessor(IServiceProvider services, ILogger<OutboxProcessor> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor started.");
        using var timer = new PeriodicTimer(_interval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessBatchAsync(stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        IReadOnlyList<OutboxMessage> pending;
        try
        {
            pending = await outbox.GetPendingAsync(BatchSize, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve pending outbox messages.");
            return;
        }

        foreach (var message in pending)
        {
            await ProcessMessageAsync(message, emailSender, uow, ct);
        }
    }

    private async Task ProcessMessageAsync(
        OutboxMessage message,
        IEmailSender emailSender,
        IUnitOfWork uow,
        CancellationToken ct)
    {
        message.MarkProcessing();
        await uow.SaveChangesAsync(ct);

        try
        {
            var email = BuildEmail(message);
            if (email is not null)
                await emailSender.SendAsync(email, ct);

            message.MarkSent();
            _logger.LogInformation("Outbox message {Id} ({Type}) sent.", message.Id, message.Type);
        }
        catch (Exception ex)
        {
            message.MarkFailed(ex.Message, MaxAttempts);
            _logger.LogWarning(ex, "Outbox message {Id} ({Type}) failed. Attempt {Attempt}.",
                message.Id, message.Type, message.AttemptCount);
        }

        await uow.SaveChangesAsync(ct);
    }

    private static EmailMessage? BuildEmail(OutboxMessage message)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(message.Payload);
        var root = doc.RootElement;

        return message.Type switch
        {
            "booking.created" => new EmailMessage(
                To: root.GetProperty("CustomerEmail").GetString() ?? string.Empty,
                Subject: "Confirme seu agendamento",
                HtmlBody: BuildBookingCreatedEmail(root)),

            "booking.confirmed" => null, // template handled by a more detailed handler
            "booking.cancelled" => null,

            _ => null
        };
    }

    private static string BuildBookingCreatedEmail(System.Text.Json.JsonElement root)
    {
        var customerName = root.TryGetProperty("CustomerName", out var cn) ? cn.GetString() : "Cliente";
        var serviceName = root.TryGetProperty("ServiceName", out var sn) ? sn.GetString() : "serviço";
        var professionalName = root.TryGetProperty("ProfessionalName", out var pn) ? pn.GetString() : "";

        return $$"""
            <h2>Olá, {{customerName}}!</h2>
            <p>Seu agendamento de <strong>{{serviceName}}</strong> com <strong>{{professionalName}}</strong> foi recebido.</p>
            <p>Clique no link abaixo para confirmar:</p>
            <p><a href="{CONFIRMATION_LINK}">Confirmar agendamento</a></p>
            <p>O link expira em 48 horas.</p>
            """;
    }
}
