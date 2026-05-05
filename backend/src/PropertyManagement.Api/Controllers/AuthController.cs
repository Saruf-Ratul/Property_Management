using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PropertyManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IValidator<LoginRequest> _loginVal;
    private readonly IValidator<RegisterRequest> _regVal;
    private readonly ICurrentUser _current;

    public AuthController(IAuthService auth, IValidator<LoginRequest> loginVal, IValidator<RegisterRequest> regVal, ICurrentUser current)
    {
        _auth = auth; _loginVal = loginVal; _regVal = regVal; _current = current;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        await _loginVal.ValidateAndThrowAsync(req, ct);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = HttpContext.Request.Headers.UserAgent.ToString();
        var result = await _auth.LoginAsync(req, ip, ua, ct);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized(new { error = result.Error });
    }

    [HttpPost("register")]
    [Authorize(Roles = Roles.FirmAdmin)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        await _regVal.ValidateAndThrowAsync(req, ct);
        if (_current.LawFirmId is null) return Unauthorized();
        var result = await _auth.RegisterAsync(req, _current.LawFirmId.Value, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var me = await _auth.GetCurrentAsync(ct);
        return me is null ? Unauthorized() : Ok(me);
    }
}
