using AgendaFlow.Application.Contracts;
using AgendaFlow.Application.DTOs;
using AgendaFlow.Domain.Entities;
using AgendaFlow.Domain.Enums;
using AgendaFlow.Infrastructure.Identity;
using AgendaFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AgendaFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailSender _emailSender;
    private readonly IMembershipRepository _memberships;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailSender emailSender,
        IMembershipRepository memberships,
        IUnitOfWork uow,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
        _memberships = memberships;
        _uow = uow;
        _logger = logger;
    }

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FullName = request.FullName.Trim()
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            // Return generic error to prevent user enumeration
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        // Send confirmation email
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmUrl = Url.Action(nameof(ConfirmEmail), "Auth",
            new { userId = user.Id, token }, Request.Scheme);

        await _emailSender.SendAsync(new EmailMessage(
            normalizedEmail,
            "Confirme seu e-mail — AgendaFlow",
            $"<p>Olá {request.FullName}!</p><p><a href='{confirmUrl}'>Confirme seu e-mail</a></p>"), ct);

        _logger.LogInformation("New user registered: {UserId}", user.Id);

        return StatusCode(StatusCodes.Status201Created, new { message = "Conta criada. Verifique seu e-mail para confirmar." });
    }

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var result = await _signInManager.PasswordSignInAsync(normalizedEmail, request.Password,
            isPersistent: false, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account locked out: {Email}", normalizedEmail);
            return Unauthorized(new { message = "Conta bloqueada temporariamente. Tente novamente em alguns minutos." });
        }

        if (!result.Succeeded)
        {
            // Avoid revealing whether the email exists (anti-enumeration)
            return Unauthorized(new { message = "E-mail ou senha inválidos." });
        }

        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        var memberships = await _memberships.ListByUserAsync(user!.Id, default);

        return Ok(new
        {
            userId = user.Id,
            email = user.Email,
            fullName = user.FullName,
            tenants = memberships.Select(m => new
            {
                tenantId = m.TenantId,
                tenantName = m.Tenant?.Name,
                role = m.Role.ToString()
            })
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string token)
    {
        var user = await _userManager.FindByIdAsync(userId);
        // Same response whether user exists or not (anti-enumeration)
        if (user is null)
            return Ok(new { message = "E-mail confirmado." });

        var result = await _userManager.ConfirmEmailAsync(user, token);
        return Ok(new { message = "E-mail confirmado." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        // Always return the same response — do not reveal if email is registered
        const string genericMsg = "Se o e-mail estiver cadastrado, você receberá as instruções em breve.";

        if (user is not null && await _userManager.IsEmailConfirmedAsync(user))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetUrl = $"{Request.Scheme}://{Request.Host}/reset-password?email={Uri.EscapeDataString(normalizedEmail)}&token={Uri.EscapeDataString(token)}";

            await _emailSender.SendAsync(new EmailMessage(
                normalizedEmail,
                "Redefinição de senha — AgendaFlow",
                $"<p><a href='{resetUrl}'>Clique aqui para redefinir sua senha</a></p><p>O link expira em 1 hora.</p>"), ct);
        }

        return Ok(new { message = genericMsg });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
        const string genericMsg = "Senha redefinida com sucesso.";

        if (user is null)
            return Ok(new { message = genericMsg });

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        // Invalidate all existing sessions
        await _userManager.UpdateSecurityStampAsync(user);

        return Ok(new { message = genericMsg });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = _userManager.GetUserId(User);
        var user = await _userManager.FindByIdAsync(userId!);
        if (user is null) return Unauthorized();

        var memberships = await _memberships.ListByUserAsync(user.Id, default);
        return Ok(new
        {
            userId = user.Id,
            email = user.Email,
            fullName = user.FullName,
            tenants = memberships.Select(m => new
            {
                tenantId = m.TenantId,
                tenantName = m.Tenant?.Name,
                role = m.Role.ToString()
            })
        });
    }
}
