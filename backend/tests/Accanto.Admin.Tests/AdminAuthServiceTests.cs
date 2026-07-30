using Accanto.Admin.Application.Auth;
using Accanto.Admin.Application.Common;
using Accanto.Admin.Application.Common.Security;
using Accanto.Admin.Domain.Authorization;
using Accanto.Admin.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Accanto.Admin.Tests;

public class AdminAuthServiceTests
{
    private static AdminAuthService Create(Accanto.Admin.Infrastructure.Persistence.AccantoAdminDbContext db, NoOpAdminAuditLog audit)
    {
        var opt = Options.Create(new AdminJwtOptions { ExpiryMinutes = 60, RefreshTokenExpiryDays = 7 });
        return new AdminAuthService(db, new FakeAdminPasswordHasher(), new FakeAdminJwtTokenService(), audit, opt, TimeProvider.System);
    }

    private static AdminUser SeedAdmin(Accanto.Admin.Infrastructure.Persistence.AccantoAdminDbContext db, string email, string password, bool active = true, string role = AdminRoles.Owner)
    {
        var hasher = new FakeAdminPasswordHasher();
        var adminRole = new AdminRole { Id = Guid.NewGuid(), Name = role };
        db.AdminRoles.Add(adminRole);
        var admin = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Admin",
            PasswordHash = hasher.Hash(password),
            IsActive = active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        admin.Roles.Add(new AdminUserRole { Id = Guid.NewGuid(), AdminUserId = admin.Id, AdminRoleId = adminRole.Id });
        db.AdminUsers.Add(admin);
        return admin;
    }

    [Fact]
    public async Task Login_succeeds_with_valid_credentials()
    {
        using var db = AdminTestDb.Create();
        SeedAdmin(db, "admin@example.com", "secret");
        await db.SaveChangesAsync();
        var audit = new NoOpAdminAuditLog();
        var svc = Create(db, audit);

        var res = await svc.LoginAsync(new AdminLoginRequest("admin@example.com", "secret"));

        res.AccessToken.Should().NotBeNullOrWhiteSpace();
        res.RefreshToken.Should().NotBeNullOrWhiteSpace();
        res.AdminUser.Email.Should().Be("admin@example.com");
        res.AdminUser.Roles.Should().Contain(AdminRoles.Owner);
        audit.Calls.Should().Contain(c => c.Action == "Admin.Login");
    }

    [Fact]
    public async Task Login_fails_with_invalid_password()
    {
        using var db = AdminTestDb.Create();
        SeedAdmin(db, "admin@example.com", "secret");
        await db.SaveChangesAsync();
        var svc = Create(db, new NoOpAdminAuditLog());

        await FluentActions.Invoking(() => svc.LoginAsync(new AdminLoginRequest("admin@example.com", "wrong")))
            .Should().ThrowAsync<AdminUnauthorizedException>();
    }

    [Fact]
    public async Task Login_fails_for_inactive_admin()
    {
        using var db = AdminTestDb.Create();
        SeedAdmin(db, "admin@example.com", "secret", active: false);
        await db.SaveChangesAsync();
        var svc = Create(db, new NoOpAdminAuditLog());

        await FluentActions.Invoking(() => svc.LoginAsync(new AdminLoginRequest("admin@example.com", "secret")))
            .Should().ThrowAsync<AdminUnauthorizedException>();
    }

    [Fact]
    public async Task Login_fails_for_unknown_email()
    {
        using var db = AdminTestDb.Create();
        var svc = Create(db, new NoOpAdminAuditLog());

        await FluentActions.Invoking(() => svc.LoginAsync(new AdminLoginRequest("nobody@example.com", "secret")))
            .Should().ThrowAsync<AdminUnauthorizedException>();
    }

    [Fact]
    public async Task Refresh_token_is_stored_hashed_not_raw()
    {
        using var db = AdminTestDb.Create();
        SeedAdmin(db, "admin@example.com", "secret");
        await db.SaveChangesAsync();
        var svc = Create(db, new NoOpAdminAuditLog());

        var res = await svc.LoginAsync(new AdminLoginRequest("admin@example.com", "secret"));

        var session = await db.AdminSessions.SingleAsync();
        session.RefreshTokenHash.Should().NotBeNullOrWhiteSpace();
        session.RefreshTokenHash.Should().NotBe(res.RefreshToken, "il token raw non deve mai essere persistito");
        session.RefreshTokenHash.Should().MatchRegex("^[0-9a-f]{64}$", "deve essere un hash SHA-256 esadecimale");
    }

    [Fact]
    public async Task Refresh_succeeds_and_rotates_token()
    {
        using var db = AdminTestDb.Create();
        SeedAdmin(db, "admin@example.com", "secret");
        await db.SaveChangesAsync();
        var svc = Create(db, new NoOpAdminAuditLog());

        var login = await svc.LoginAsync(new AdminLoginRequest("admin@example.com", "secret"));
        var refreshed = await svc.RefreshAsync(new AdminRefreshRequest(login.RefreshToken));

        refreshed.AccessToken.Should().NotBeNullOrWhiteSpace();
        refreshed.RefreshToken.Should().NotBe(login.RefreshToken, "il refresh token deve ruotare");
        (await db.AdminSessions.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Refresh_fails_with_revoked_token_and_revokes_all_sessions()
    {
        using var db = AdminTestDb.Create();
        var admin = SeedAdmin(db, "admin@example.com", "secret");
        await db.SaveChangesAsync();
        var svc = Create(db, new NoOpAdminAuditLog());

        var login = await svc.LoginAsync(new AdminLoginRequest("admin@example.com", "secret"));
        // Primo refresh: valido, revoca il token originale.
        await svc.RefreshAsync(new AdminRefreshRequest(login.RefreshToken));

        // Riuso del token originale (ora revocato) → compromissione: revoca tutto.
        await FluentActions.Invoking(() => svc.RefreshAsync(new AdminRefreshRequest(login.RefreshToken)))
            .Should().ThrowAsync<AdminForbiddenException>();

        var activeSessions = await db.AdminSessions.CountAsync(s => s.AdminUserId == admin.Id && s.RevokedAt == null);
        activeSessions.Should().Be(0, "il riuso di un token revocato revoca tutte le sessioni");
    }

    [Fact]
    public async Task Refresh_fails_with_garbage_token()
    {
        using var db = AdminTestDb.Create();
        var svc = Create(db, new NoOpAdminAuditLog());

        await FluentActions.Invoking(() => svc.RefreshAsync(new AdminRefreshRequest("not-a-real-token")))
            .Should().ThrowAsync<AdminForbiddenException>();
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        using var db = AdminTestDb.Create();
        SeedAdmin(db, "admin@example.com", "secret");
        await db.SaveChangesAsync();
        var audit = new NoOpAdminAuditLog();
        var svc = Create(db, audit);

        var login = await svc.LoginAsync(new AdminLoginRequest("admin@example.com", "secret"));
        await svc.LogoutAsync(new AdminLogoutRequest(login.RefreshToken));

        var session = await db.AdminSessions.SingleAsync();
        session.RevokedAt.Should().NotBeNull();
        audit.Calls.Should().Contain(c => c.Action == "Admin.Logout");

        // Il token revocato non e' piu' utilizzabile per il refresh.
        await FluentActions.Invoking(() => svc.RefreshAsync(new AdminRefreshRequest(login.RefreshToken)))
            .Should().ThrowAsync<AdminForbiddenException>();
    }

    [Fact]
    public async Task GetMe_returns_admin_with_roles()
    {
        using var db = AdminTestDb.Create();
        var admin = SeedAdmin(db, "admin@example.com", "secret");
        await db.SaveChangesAsync();
        var svc = Create(db, new NoOpAdminAuditLog());

        var me = await svc.GetMeAsync(admin.Id);

        me.Email.Should().Be("admin@example.com");
        me.Roles.Should().Contain(AdminRoles.Owner);
    }
}
