using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.DTOs;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Identity;
using PropertyManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace PropertyManagement.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signin;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _current;
    private readonly ITenantContext _tenant;

    public AuthService(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signin, RoleManager<ApplicationRole> roles,
        AppDbContext db, IJwtService jwt, IAuditService audit, ICurrentUser current, ITenantContext tenant)
    {
        _users = users; _signin = signin; _roles = roles; _db = db; _jwt = jwt; _audit = audit; _current = current; _tenant = tenant;
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest req, string? ip, string? ua, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null)
        {
            // User not found — log as orphan event (no firm scope known).
            using (_tenant.Bypass())
                await _audit.LogLoginFailedAsync(req.Email, "User not found",
                    lawFirmId: null, ip: ip, userAgent: ua, ct: ct);
            return Result<AuthResponse>.Failure("Invalid credentials");
        }
        if (!user.IsActive)
        {
            using (_tenant.Bypass())
                await _audit.LogLoginFailedAsync(req.Email, "Account disabled",
                    lawFirmId: user.LawFirmId, ip: ip, userAgent: ua, ct: ct);
            return Result<AuthResponse>.Failure("Invalid credentials");
        }

        var ok = await _users.CheckPasswordAsync(user, req.Password);
        if (!ok)
        {
            using (_tenant.Bypass())
                await _audit.LogLoginFailedAsync(req.Email, "Wrong password",
                    lawFirmId: user.LawFirmId, ip: ip, userAgent: ua, ct: ct);
            return Result<AuthResponse>.Failure("Invalid credentials");
        }

        var roles = await _users.GetRolesAsync(user);
        var tokens = _jwt.Issue(user.Id, user.Email!, roles, user.LawFirmId, user.ClientId);

        user.RefreshToken = tokens.RefreshToken;
        user.RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(7);
        await _users.UpdateAsync(user);

        UserProfile? profile;
        using (_tenant.Bypass())
        {
            profile = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.IdentityUserId == user.Id, ct);
            await _audit.LogLoginSuccessAsync(user.Id, user.Email!, user.LawFirmId, ip, ua, ct);
        }

        var dto = new UserDto(
            profile?.Id ?? Guid.Empty,
            user.Email!,
            user.FirstName,
            user.LastName,
            $"{user.FirstName} {user.LastName}".Trim(),
            user.LawFirmId,
            user.ClientId,
            roles.ToArray());

        return Result<AuthResponse>.Success(new AuthResponse(tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresAtUtc, dto));
    }

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest req, Guid lawFirmId, CancellationToken ct = default)
    {
        if (!Roles.All.Contains(req.Role))
            return Result<AuthResponse>.Failure("Invalid role");

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            EmailConfirmed = true,
            FirstName = req.FirstName,
            LastName = req.LastName,
            LawFirmId = lawFirmId,
            ClientId = Roles.ClientStaff.Contains(req.Role) ? req.ClientId : null
        };
        var create = await _users.CreateAsync(user, req.Password);
        if (!create.Succeeded)
            return Result<AuthResponse>.Failure(string.Join(";", create.Errors.Select(e => e.Description)));

        if (!await _roles.RoleExistsAsync(req.Role))
            await _roles.CreateAsync(new ApplicationRole { Name = req.Role });
        await _users.AddToRoleAsync(user, req.Role);

        using (_tenant.Bypass())
        {
            _db.UserProfiles.Add(new UserProfile
            {
                IdentityUserId = user.Id,
                LawFirmId = lawFirmId,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                ClientId = user.ClientId,
                IsActive = true
            });
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync(AuditAction.CreateUser, nameof(ApplicationUser), user.Id,
                $"Created user {user.Email} with role {req.Role}",
                payload: new { email = req.Email, role = req.Role, lawFirmId, clientId = user.ClientId },
                ct);
        }

        return await LoginAsync(new LoginRequest(req.Email, req.Password), null, null, ct);
    }

    public async Task<UserDto?> GetCurrentAsync(CancellationToken ct = default)
    {
        if (!_current.IsAuthenticated || _current.UserId is null) return null;

        var idUser = await _users.FindByIdAsync(_current.UserId.Value.ToString());
        if (idUser is null) idUser = await _users.FindByEmailAsync(_current.Email ?? "");
        if (idUser is null) return null;

        var roles = await _users.GetRolesAsync(idUser);
        UserProfile? profile;
        using (_tenant.Bypass())
        {
            profile = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.IdentityUserId == idUser.Id, ct);
        }
        return new UserDto(
            profile?.Id ?? Guid.Empty,
            idUser.Email!,
            idUser.FirstName,
            idUser.LastName,
            $"{idUser.FirstName} {idUser.LastName}".Trim(),
            idUser.LawFirmId,
            idUser.ClientId,
            roles.ToArray());
    }
}
