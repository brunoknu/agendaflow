using AgendaFlow.Application.Contracts;
using AgendaFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace AgendaFlow.IntegrationTests;

/// <summary>
/// Shared factory: spins up one PostgreSQL container for the entire test session.
/// Implement <see cref="IAsyncLifetime"/> so xunit can start/stop the container.
/// </summary>
public sealed class AgendaFlowWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("agendaflow_test")
        .WithUsername("postgres")
        .WithPassword("test_password")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Replace real DB with Testcontainers PostgreSQL
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>((sp, opts) =>
            {
                opts.UseNpgsql(_postgres.GetConnectionString());
            });

            // Replace email sender with no-op — no real emails during tests
            services.RemoveAll<IEmailSender>();
            services.AddScoped<IEmailSender, NoOpEmailSender>();

            // Disable email confirmation requirement so tests can log in immediately
            services.PostConfigure<Microsoft.AspNetCore.Identity.IdentityOptions>(opts =>
            {
                opts.SignIn.RequireConfirmedEmail = false;
            });
        });

        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            // Suppress DataSeeder in test environment (handled by IntegrationTestBase)
        });
    }

    /// <summary>Runs EF migrations and returns a scoped service provider.</summary>
    public async Task MigrateAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}

internal sealed class NoOpEmailSender : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default) => Task.CompletedTask;
}
