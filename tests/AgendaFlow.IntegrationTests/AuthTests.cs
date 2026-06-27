using System.Net;
using System.Net.Http.Json;

namespace AgendaFlow.IntegrationTests;

public class AuthTests(AgendaFlowWebAppFactory factory) : IntegrationTestBase(factory), IClassFixture<AgendaFlowWebAppFactory>
{
    [Fact]
    public async Task Register_WithValidData_Returns201()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"newuser_{Guid.NewGuid():N}@test.com",
            password = "Test@1234",
            fullName = "New User",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithWeakPassword_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"weakpass_{Guid.NewGuid():N}@test.com",
            password = "123",
            fullName = "Weak User",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndCookie()
    {
        var email = $"login_{Guid.NewGuid():N}@test.com";
        await LoginAsAsync(email, "Test@1234");

        // Me endpoint should now work (cookie is set on Client)
        var me = await Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var email = $"wrongpass_{Guid.NewGuid():N}@test.com";
        await LoginAsAsync(email, "Test@1234"); // creates user

        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPassword!",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsSession()
    {
        var email = $"logout_{Guid.NewGuid():N}@test.com";
        await LoginAsAsync(email, "Test@1234");

        // Logout
        await PostJsonWithCsrfAsync("/api/auth/logout", new { });

        // Me should now return 401
        var me = await Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }
}
