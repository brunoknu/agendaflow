// Requires: MailKit package
// dotnet add package MailKit

using AgendaFlow.Application.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MailKit.Net.Smtp;

namespace AgendaFlow.Infrastructure.Email;

public sealed class MailKitEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(EmailOptions options, ILogger<MailKitEmailSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.SenderName, _options.SenderEmail));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = message.HtmlBody };

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, _options.UseSsl, ct);

            if (!string.IsNullOrEmpty(_options.Username))
                await client.AuthenticateAsync(_options.Username, _options.Password, ct);

            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(quit: true, ct);

            _logger.LogInformation("Email sent to {To} — Subject: {Subject}", message.To, message.Subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", message.To);
            throw;
        }
    }
}

public sealed class EmailOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1025;
    public bool UseSsl { get; init; } = false;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string SenderEmail { get; init; } = "noreply@agendaflow.local";
    public string SenderName { get; init; } = "AgendaFlow";
}
