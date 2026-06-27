using System.Threading.RateLimiting;
using AgendaFlow.Api.Middleware;
using AgendaFlow.Application.Appointments;
using AgendaFlow.Infrastructure;
using AgendaFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Infrastructure (DB, Identity, Repositories, Email, etc.) ──
builder.Services.AddInfrastructure(builder.Configuration);

// ── Application services ───────────────────────────────────────
builder.Services.AddScoped<AppointmentService>();

// ── Controllers ────────────────────────────────────────────────
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(opts =>
    {
        opts.InvalidModelStateResponseFactory = ctx =>
        {
            var errors = ctx.ModelState
                .Where(m => m.Value?.Errors.Count > 0)
                .Select(m => new { field = m.Key, errors = m.Value!.Errors.Select(e => e.ErrorMessage) });
            return new Microsoft.AspNetCore.Mvc.UnprocessableEntityObjectResult(new
            {
                title = "Validation failed",
                status = 422,
                errors
            });
        };
    });

// ── CORS ───────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowCredentials()
              .AllowAnyHeader()
              .AllowAnyMethod()));

// ── CSRF ───────────────────────────────────────────────────────
builder.Services.AddAntiforgery(opts =>
{
    opts.HeaderName = "X-XSRF-TOKEN";
    opts.Cookie.Name = "XSRF-TOKEN";
    opts.Cookie.HttpOnly = false;
    opts.Cookie.SameSite = SameSiteMode.Strict;
    opts.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// ── Cookie auth settings ───────────────────────────────────────
builder.Services.ConfigureApplicationCookie(opts =>
{
    opts.Cookie.HttpOnly = true;
    opts.Cookie.SameSite = SameSiteMode.Strict;
    opts.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    opts.ExpireTimeSpan = TimeSpan.FromHours(8);
    opts.SlidingExpiration = true;
    opts.LoginPath = null;
    opts.AccessDeniedPath = null;
    opts.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
    opts.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
});

// ── Rate limiting ──────────────────────────────────────────────
builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opts.AddFixedWindowLimiter("auth", o => { o.Window = TimeSpan.FromMinutes(1); o.PermitLimit = 5; o.QueueLimit = 0; });
    opts.AddFixedWindowLimiter("recovery", o => { o.Window = TimeSpan.FromMinutes(15); o.PermitLimit = 3; o.QueueLimit = 0; });
    opts.AddFixedWindowLimiter("public-booking", o => { o.Window = TimeSpan.FromMinutes(1); o.PermitLimit = 10; o.QueueLimit = 0; });
    opts.AddFixedWindowLimiter("api", o => { o.Window = TimeSpan.FromMinutes(1); o.PermitLimit = 120; o.QueueLimit = 0; });
});

// ── Health checks ──────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AgendaFlow.Infrastructure.Persistence.AppDbContext>("database");

// ── Swagger ────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
    opts.SwaggerDoc("v1", new() { Title = "AgendaFlow API", Version = "v1" }));

var app = builder.Build();

// ── Development seed ───────────────────────────────────────────
if (app.Environment.IsDevelopment())
    await DataSeeder.SeedAsync(app.Services);

// ── Security headers ───────────────────────────────────────────
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; font-src 'self'; frame-ancestors 'none'; base-uri 'self'";
    if (!app.Environment.IsDevelopment())
        ctx.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    await next();
});

app.UseGlobalExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseTenantResolution();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapControllers();

app.MapGet("/api/antiforgery/token", (IAntiforgery antiforgery, HttpContext ctx) =>
{
    var tokens = antiforgery.GetAndStoreTokens(ctx);
    return Results.Ok(new { token = tokens.RequestToken });
}).RequireAuthorization();

app.Run();

public partial class Program { }
