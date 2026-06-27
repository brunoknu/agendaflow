using System.Net;
using System.Net.Http.Json;

namespace AgendaFlow.IntegrationTests;

public class ServicesTests(AgendaFlowWebAppFactory factory) : IntegrationTestBase(factory), IClassFixture<AgendaFlowWebAppFactory>
{
    private async Task SetupUserWithTenantAsync()
    {
        var email = $"svc_{Guid.NewGuid():N}@test.com";
        var userId = await LoginAsAsync(email, "Test@1234", "Services User");
        await CreateTenantForUserAsync(userId, "Test Clinic", $"testclinic-{Guid.NewGuid():N}");
    }

    [Fact]
    public async Task GetServices_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/services");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateService_WithValidData_Returns201()
    {
        await SetupUserWithTenantAsync();

        var response = await PostJsonWithCsrfAsync("/api/services", new
        {
            name = "Consulta",
            durationMinutes = 30,
            price = 150.00,
            currency = "BRL",
            bufferBeforeMinutes = 0,
            bufferAfterMinutes = 5,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ServiceResponse>();
        Assert.NotNull(body);
        Assert.Equal("Consulta", body.Name);
        Assert.Equal(30, body.DurationMinutes);
        Assert.True(body.IsActive);
    }

    [Fact]
    public async Task GetServices_AfterCreate_ReturnsCreatedService()
    {
        await SetupUserWithTenantAsync();

        var name = $"Service-{Guid.NewGuid():N[..8]}";
        await PostJsonWithCsrfAsync("/api/services", new
        {
            name,
            durationMinutes = 45,
            price = 200.00,
            currency = "BRL",
            bufferBeforeMinutes = 0,
            bufferAfterMinutes = 0,
        });

        var list = await Client.GetFromJsonAsync<List<ServiceResponse>>("/api/services");
        Assert.NotNull(list);
        Assert.Contains(list, s => s.Name == name);
    }

    [Fact]
    public async Task UpdateService_WithValidData_ReturnsUpdated()
    {
        await SetupUserWithTenantAsync();

        // Create service first
        var createRes = await PostJsonWithCsrfAsync("/api/services", new
        {
            name = "Original Name",
            durationMinutes = 30,
            price = 100.00,
            currency = "BRL",
            bufferBeforeMinutes = 0,
            bufferAfterMinutes = 0,
        });
        var created = await createRes.Content.ReadFromJsonAsync<ServiceResponse>();
        Assert.NotNull(created);

        // Update it
        var updateRes = await PutJsonWithCsrfAsync($"/api/services/{created.Id}", new
        {
            name = "Updated Name",
            durationMinutes = 45,
            price = 120.00,
            bufferBeforeMinutes = 5,
            bufferAfterMinutes = 5,
        });

        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updated = await updateRes.Content.ReadFromJsonAsync<ServiceResponse>();
        Assert.Equal("Updated Name", updated?.Name);
        Assert.Equal(45, updated?.DurationMinutes);
    }

    [Fact]
    public async Task DeactivateService_Returns204()
    {
        await SetupUserWithTenantAsync();

        var createRes = await PostJsonWithCsrfAsync("/api/services", new
        {
            name = "To Deactivate",
            durationMinutes = 30,
            price = 100.00,
            currency = "BRL",
            bufferBeforeMinutes = 0,
            bufferAfterMinutes = 0,
        });
        var created = await createRes.Content.ReadFromJsonAsync<ServiceResponse>();
        Assert.NotNull(created);

        var deactivateRes = await PatchWithCsrfAsync($"/api/services/{created.Id}/deactivate", new { });
        Assert.Equal(HttpStatusCode.NoContent, deactivateRes.StatusCode);

        // List with active filter — should not contain deactivated service
        var active = await Client.GetFromJsonAsync<List<ServiceResponse>>("/api/services?active=true");
        Assert.NotNull(active);
        Assert.DoesNotContain(active, s => s.Id == created.Id);
    }

    private record ServiceResponse(Guid Id, string Name, int DurationMinutes, decimal Price, bool IsActive);
}
