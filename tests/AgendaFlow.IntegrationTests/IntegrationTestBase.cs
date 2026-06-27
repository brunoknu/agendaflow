using System.Net.Http.Json;
using AgendaFlow.Domain.Entities;
using AgendaFlow.Domain.Enums;
using AgendaFlow.Infrastructure.Identity;
using AgendaFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AgendaFlow.IntegrationTests;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly AgendaFlowWebAppFactory Factory;
    protected HttpClient Client = default!;

    protected IntegrationTestBase(AgendaFlowWebAppFactory factory)
    {
        Factory = factory;
    }

    public virtual async Task InitializeAsync()
    {
        await Factory.MigrateAsync();
        Client = Factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    protected async Task<string> LoginAsAsync(string email, string password, string fullName = "Test User")
    {
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, FullName = fullName, EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Could not create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        var response = await Client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return user.Id;
    }

    protected async Task<Guid> CreateTenantForUserAsync(string userId, string name, string slug)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = Tenant.Create(name, slug, "America/Sao_Paulo");
        db.Tenants.Add(tenant);
        db.TenantMemberships.Add(TenantMembership.Create(tenant.Id, userId, MembershipRole.Owner));
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    protected async Task<HttpResponseMessage> PostJsonWithCsrfAsync<T>(string url, T body)
    {
        // Fetch CSRF token (requires auth cookie already set)
        var tokenRes = await Client.GetFromJsonAsync<CsrfResponse>("/api/antiforgery/token");
        var token = tokenRes?.Token ?? string.Empty;

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = JsonContent.Create(body);
        req.Headers.Add("X-XSRF-TOKEN", token);
        return await Client.SendAsync(req);
    }

    protected async Task<HttpResponseMessage> PutJsonWithCsrfAsync<T>(string url, T body)
    {
        var tokenRes = await Client.GetFromJsonAsync<CsrfResponse>("/api/antiforgery/token");
        var token = tokenRes?.Token ?? string.Empty;

        using var req = new HttpRequestMessage(HttpMethod.Put, url);
        req.Content = JsonContent.Create(body);
        req.Headers.Add("X-XSRF-TOKEN", token);
        return await Client.SendAsync(req);
    }

    protected async Task<HttpResponseMessage> PatchWithCsrfAsync<T>(string url, T body)
    {
        var tokenRes = await Client.GetFromJsonAsync<CsrfResponse>("/api/antiforgery/token");
        var token = tokenRes?.Token ?? string.Empty;

        using var req = new HttpRequestMessage(HttpMethod.Patch, url);
        req.Content = JsonContent.Create(body);
        req.Headers.Add("X-XSRF-TOKEN", token);
        return await Client.SendAsync(req);
    }

    private record CsrfResponse(string Token);
}
