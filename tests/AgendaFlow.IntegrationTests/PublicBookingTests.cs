using System.Net;
using System.Net.Http.Json;
using AgendaFlow.Domain.Entities;
using AgendaFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgendaFlow.IntegrationTests;

public class PublicBookingTests(AgendaFlowWebAppFactory factory) : IntegrationTestBase(factory), IClassFixture<AgendaFlowWebAppFactory>
{
    private const string Slug = "clinica-test";

    private async Task SeedTenantDataAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Idempotent: skip if tenant already seeded
        if (await db.Tenants.AnyAsync(t => t.Slug == Slug))
            return;

        var tenant = Tenant.Create("Clínica Test", Slug, "America/Sao_Paulo");
        db.Tenants.Add(tenant);

        var service = Service.Create(tenant.Id, "Consulta", null, 30, 100m);
        db.Services.Add(service);

        var professional = Professional.Create(tenant.Id, "Dr. Teste", null, null);
        db.Professionals.Add(professional);
        await db.SaveChangesAsync();

        db.ProfessionalServices.Add(ProfessionalService.Create(professional.Id, service.Id));

        // Add availability rule: Monday–Friday 08:00–18:00
        for (var dow = DayOfWeek.Monday; dow <= DayOfWeek.Friday; dow++)
        {
            db.AvailabilityRules.Add(AvailabilityRule.Create(
                tenant.Id, professional.Id, dow,
                new TimeOnly(8, 0), new TimeOnly(18, 0), 30));
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetServices_ForValidSlug_ReturnsActiveServices()
    {
        await SeedTenantDataAsync();

        var response = await Client.GetAsync($"/api/public/{Slug}/services");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var services = await response.Content.ReadFromJsonAsync<List<ServiceItem>>();
        Assert.NotNull(services);
        Assert.NotEmpty(services);
    }

    [Fact]
    public async Task GetServices_ForInvalidSlug_Returns404()
    {
        var response = await Client.GetAsync("/api/public/slug-that-does-not-exist/services");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProfessionals_ForValidSlug_ReturnsProfessionals()
    {
        await SeedTenantDataAsync();

        var response = await Client.GetAsync($"/api/public/{Slug}/professionals");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var professionals = await response.Content.ReadFromJsonAsync<List<ProfessionalItem>>();
        Assert.NotNull(professionals);
        Assert.NotEmpty(professionals);
    }

    [Fact]
    public async Task GetAvailability_ForValidInputs_ReturnsSlots()
    {
        await SeedTenantDataAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = await db.Tenants.FirstAsync(t => t.Slug == Slug);
        var service = await db.Services.FirstAsync(s => s.TenantId == tenant.Id);
        var professional = await db.Professionals.FirstAsync(p => p.TenantId == tenant.Id);

        // Find next Monday
        var today = DateOnly.FromDateTime(DateTime.Today);
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7;
        var nextMonday = today.AddDays(daysUntilMonday);

        var url = $"/api/public/{Slug}/availability?professionalId={professional.Id}&serviceId={service.Id}&date={nextMonday:yyyy-MM-dd}";
        var response = await Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AvailabilityResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body.Slots);
    }

    private record ServiceItem(Guid Id, string Name, int DurationMinutes, decimal Price);
    private record ProfessionalItem(Guid Id, string Name);
    private record AvailabilityResponse(List<string> Slots);
}
