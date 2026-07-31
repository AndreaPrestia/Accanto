using Accanto.Admin.Application.Auth;
using Accanto.Admin.Application.Common;
using Accanto.Admin.Application.Email;
using Accanto.Admin.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Accanto.Admin.Tests;

public class AdminPasswordResetServiceTests
{
    private sealed class FakeEmailSender : IAdminEmailSender
    {
        public bool IsConfigured => true;
        public List<(string To, string Subject, string Html)> Sent { get; } = new();
        public Task SendAsync(string recipientEmail, string? recipientDisplayName, string subject, string htmlBody, CancellationToken ct = default)
        {
            Sent.Add((recipientEmail, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    private static AdminPasswordResetService Create(
        Accanto.Admin.Infrastructure.Persistence.AccantoAdminDbContext db,
        FakeEmailSender email,
        NoOpAdminAuditLog audit)
    {
        var opt = Options.Create(new AdminPasswordResetOptions { PublicUrl = "https://admin.test", ResetPath = "/reset-password", TokenLifetimeMinutes = 60 });
        return new AdminPasswordResetService(db, new FakeAdminPasswordHasher(), email, audit, opt, TimeProvider.System, NullLogger<AdminPasswordResetService>.Instance);
    }

    private static AdminUser SeedAdmin(Accanto.Admin.Infrastructure.Persistence.AccantoAdminDbContext db, string email, bool active = true, string passwordHash = "")
    {
        var admin = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Admin",
            PasswordHash = passwordHash,
            IsActive = active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.AdminUsers.Add(admin);
        return admin;
    }

    [Fact]
    public async Task Request_issues_hashed_token_and_sends_email()
    {
        using var db = AdminTestDb.Create();
        var admin = SeedAdmin(db, "admin@example.com");
        await db.SaveChangesAsync();
        var email = new FakeEmailSender();
        var svc = Create(db, email, new NoOpAdminAuditLog());

        await svc.RequestResetAsync(new AdminForgotPasswordRequest("admin@example.com"));

        var token = await db.AdminPasswordResetTokens.SingleAsync();
        token.AdminUserId.Should().Be(admin.Id);
        token.TokenHash.Should().MatchRegex("^[0-9a-f]{64}$");
        token.UsedAt.Should().BeNull();
        email.Sent.Should().ContainSingle();
        email.Sent[0].To.Should().Be("admin@example.com");
        // Il link contiene il token raw, ma il DB no.
        email.Sent[0].Html.Should().Contain("https://admin.test/reset-password?token=");
    }

    [Fact]
    public async Task Request_unknown_email_is_anti_enumeration_no_token_no_throw()
    {
        using var db = AdminTestDb.Create();
        var email = new FakeEmailSender();
        var svc = Create(db, email, new NoOpAdminAuditLog());

        await svc.RequestResetAsync(new AdminForgotPasswordRequest("nobody@example.com"));

        (await db.AdminPasswordResetTokens.CountAsync()).Should().Be(0);
        email.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Request_inactive_admin_issues_no_token()
    {
        using var db = AdminTestDb.Create();
        SeedAdmin(db, "admin@example.com", active: false);
        await db.SaveChangesAsync();
        var svc = Create(db, new FakeEmailSender(), new NoOpAdminAuditLog());

        await svc.RequestResetAsync(new AdminForgotPasswordRequest("admin@example.com"));

        (await db.AdminPasswordResetTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Reset_with_valid_token_sets_password_marks_used_and_audits()
    {
        using var db = AdminTestDb.Create();
        var admin = SeedAdmin(db, "admin@example.com"); // password vuota (seedato)
        await db.SaveChangesAsync();
        var email = new FakeEmailSender();
        var audit = new NoOpAdminAuditLog();
        var svc = Create(db, email, audit);

        await svc.RequestResetAsync(new AdminForgotPasswordRequest("admin@example.com"));
        var rawToken = ExtractTokenFromLink(email.Sent[0].Html);

        await svc.ResetAsync(new AdminResetPasswordRequest(rawToken, "newStrongPassword"));

        var reloaded = await db.AdminUsers.FindAsync(admin.Id);
        reloaded!.PasswordHash.Should().Be("hashed:newStrongPassword");
        (await db.AdminPasswordResetTokens.SingleAsync()).UsedAt.Should().NotBeNull();
        audit.Calls.Should().Contain(c => c.Action == "Admin.PasswordResetCompleted");
    }

    [Fact]
    public async Task Reset_revokes_all_admin_sessions()
    {
        using var db = AdminTestDb.Create();
        var admin = SeedAdmin(db, "admin@example.com");
        db.AdminSessions.Add(new AdminSession
        {
            Id = Guid.NewGuid(), AdminUserId = admin.Id, RefreshTokenHash = "h",
            CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        });
        await db.SaveChangesAsync();
        var email = new FakeEmailSender();
        var svc = Create(db, email, new NoOpAdminAuditLog());

        await svc.RequestResetAsync(new AdminForgotPasswordRequest("admin@example.com"));
        var rawToken = ExtractTokenFromLink(email.Sent[0].Html);
        await svc.ResetAsync(new AdminResetPasswordRequest(rawToken, "newStrongPassword"));

        (await db.AdminSessions.CountAsync(s => s.RevokedAt == null)).Should().Be(0);
    }

    [Fact]
    public async Task Reset_with_used_token_fails()
    {
        using var db = AdminTestDb.Create();
        SeedAdmin(db, "admin@example.com");
        await db.SaveChangesAsync();
        var email = new FakeEmailSender();
        var svc = Create(db, email, new NoOpAdminAuditLog());

        await svc.RequestResetAsync(new AdminForgotPasswordRequest("admin@example.com"));
        var rawToken = ExtractTokenFromLink(email.Sent[0].Html);
        await svc.ResetAsync(new AdminResetPasswordRequest(rawToken, "newStrongPassword"));

        await FluentActions.Invoking(() => svc.ResetAsync(new AdminResetPasswordRequest(rawToken, "anotherPassword")))
            .Should().ThrowAsync<AdminForbiddenException>();
    }

    [Fact]
    public async Task Reset_with_garbage_token_fails()
    {
        using var db = AdminTestDb.Create();
        var svc = Create(db, new FakeEmailSender(), new NoOpAdminAuditLog());

        await FluentActions.Invoking(() => svc.ResetAsync(new AdminResetPasswordRequest("garbage", "newStrongPassword")))
            .Should().ThrowAsync<AdminForbiddenException>();
    }

    [Fact]
    public async Task Reset_with_short_password_fails_validation()
    {
        using var db = AdminTestDb.Create();
        SeedAdmin(db, "admin@example.com");
        await db.SaveChangesAsync();
        var email = new FakeEmailSender();
        var svc = Create(db, email, new NoOpAdminAuditLog());

        await svc.RequestResetAsync(new AdminForgotPasswordRequest("admin@example.com"));
        var rawToken = ExtractTokenFromLink(email.Sent[0].Html);

        await FluentActions.Invoking(() => svc.ResetAsync(new AdminResetPasswordRequest(rawToken, "short")))
            .Should().ThrowAsync<AdminValidationException>();
    }

    private static string ExtractTokenFromLink(string html)
    {
        var marker = "token=";
        var i = html.IndexOf(marker, StringComparison.Ordinal);
        var start = i + marker.Length;
        var end = html.IndexOf('"', start);
        if (end < 0) end = html.Length;
        var raw = html[start..end].Trim();
        return Uri.UnescapeDataString(raw);
    }
}
