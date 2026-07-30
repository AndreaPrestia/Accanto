using Accanto.Admin.Application.Audit;
using Accanto.Admin.Domain.Entities;
using FluentAssertions;

namespace Accanto.Admin.Tests;

public class AdminAuditQueryServiceTests
{
    private static AdminUser SeedAdmin(Accanto.Admin.Infrastructure.Persistence.AccantoAdminDbContext db, string email = "admin@example.com")
    {
        var admin = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Admin",
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.AdminUsers.Add(admin);
        return admin;
    }

    private static AdminAuditLog Entry(Guid adminId, string action, string targetType, string? targetId = null, string? reason = null, DateTimeOffset? at = null)
        => new()
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Reason = reason,
            IpAddress = "127.0.0.1",
            UserAgent = "test",
            CreatedAt = at ?? DateTimeOffset.UtcNow
        };

    [Fact]
    public async Task List_returns_entries_with_admin_email()
    {
        using var db = AdminTestDb.Create();
        var admin = SeedAdmin(db);
        db.AdminAuditLogs.Add(Entry(admin.Id, "User.Disable", "User", Guid.NewGuid().ToString(), "Requested by user."));
        await db.SaveChangesAsync();
        var svc = new AdminAuditQueryService(db);

        var result = await svc.ListAsync(null, null, null, null, null, null, 1, 20);

        result.Total.Should().Be(1);
        var item = result.Items[0];
        item.Action.Should().Be("User.Disable");
        item.AdminEmail.Should().Be("admin@example.com");
        item.Reason.Should().Be("Requested by user.");
    }

    [Fact]
    public async Task List_filters_by_action_targettype_and_admin()
    {
        using var db = AdminTestDb.Create();
        var admin = SeedAdmin(db);
        db.AdminAuditLogs.Add(Entry(admin.Id, "User.Disable", "User"));
        db.AdminAuditLogs.Add(Entry(admin.Id, "User.Enable", "User"));
        db.AdminAuditLogs.Add(Entry(admin.Id, "Admin.Login", "AdminUser"));
        await db.SaveChangesAsync();
        var svc = new AdminAuditQueryService(db);

        (await svc.ListAsync(null, "User.Disable", null, null, null, null, 1, 20)).Total.Should().Be(1);
        (await svc.ListAsync(null, null, "AdminUser", null, null, null, 1, 20)).Total.Should().Be(1);
        (await svc.ListAsync(admin.Id, null, null, null, null, null, 1, 20)).Total.Should().Be(3);
        (await svc.ListAsync(Guid.NewGuid(), null, null, null, null, null, 1, 20)).Total.Should().Be(0);
    }

    [Fact]
    public async Task List_filters_by_date_range()
    {
        using var db = AdminTestDb.Create();
        var admin = SeedAdmin(db);
        var now = DateTimeOffset.UtcNow;
        db.AdminAuditLogs.Add(Entry(admin.Id, "A", "User", at: now.AddDays(-10)));
        db.AdminAuditLogs.Add(Entry(admin.Id, "B", "User", at: now.AddDays(-1)));
        await db.SaveChangesAsync();
        var svc = new AdminAuditQueryService(db);

        var recent = await svc.ListAsync(null, null, null, null, now.AddDays(-2), null, 1, 20);
        recent.Total.Should().Be(1);
        recent.Items[0].Action.Should().Be("B");
    }

    [Fact]
    public async Task List_paginates()
    {
        using var db = AdminTestDb.Create();
        var admin = SeedAdmin(db);
        for (var i = 0; i < 25; i++)
            db.AdminAuditLogs.Add(Entry(admin.Id, $"Action{i}", "User"));
        await db.SaveChangesAsync();
        var svc = new AdminAuditQueryService(db);

        var page2 = await svc.ListAsync(null, null, null, null, null, null, 2, 10);
        page2.Total.Should().Be(25);
        page2.Items.Should().HaveCount(10);
        page2.Page.Should().Be(2);
    }

    [Fact]
    public void AuditLogDto_excludes_sensitive_payload_fields()
    {
        // L'audit DTO non deve avere proprieta' che potrebbero veicolare payload
        // sensibili (body, contenuti utente, filename, ecc.).
        var props = typeof(AdminAuditLogDto).GetProperties().Select(p => p.Name).ToList();
        props.Should().NotContain(new[] { "Body", "RequestBody", "ResponseBody", "Payload", "Content", "Title", "FileName", "OriginalFileName" });
    }

    [Fact]
    public async Task Serialized_audit_entry_contains_no_user_content()
    {
        using var db = AdminTestDb.Create();
        var admin = SeedAdmin(db);
        // Anche se un reason contenesse testo, i campi vietati non esistono nel DTO.
        db.AdminAuditLogs.Add(Entry(admin.Id, "User.Disable", "User", Guid.NewGuid().ToString(), "Requested by user."));
        await db.SaveChangesAsync();
        var svc = new AdminAuditQueryService(db);

        var result = await svc.ListAsync(null, null, null, null, null, null, 1, 20);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Items[0]);

        json.Should().NotContain("RequestBody");
        json.Should().NotContain("ResponseBody");
        json.Should().NotContain("Payload");
    }
}
