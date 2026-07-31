using System.Reflection;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Internal;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Accanto.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Accanto.Tests;

public class InternalUserMetadataTests
{
    // Campi vietati dalla privacy boundary (03-privacy-boundary.md): non devono
    // MAI comparire come proprieta' dei DTO interni esposti al control plane admin.
    private static readonly string[] ForbiddenPropertyNames =
    {
        "Name",              // CareCircle.Name
        "Description",       // CareCircle.Description
        "Title",             // TimelineEntry.Title
        "Content",           // TimelineEntry.Content / SharedUpdate.Content
        "OriginalFileName",  // MedicalDocument.OriginalFileName
        "StoragePath",       // MedicalDocument.StoragePath
        "Notes",             // MedicalDocument.Notes
        "Question",          // DoctorQuestion.Question
        "AnswerNotes",       // DoctorQuestion.AnswerNotes
        "Tags",
        "FileName",
        "PasswordHash",
        "TwoFactorSecret",
        "TwoFactorPendingSecret",
        "TwoFactorRecoveryCodesJson"
    };

    [Fact]
    public void InternalUserMetadataDto_does_not_expose_forbidden_fields()
    {
        var props = typeof(InternalUserMetadataDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        foreach (var forbidden in ForbiddenPropertyNames)
        {
            props.Should().NotContain(forbidden,
                $"il DTO interno non deve esporre la proprieta' vietata '{forbidden}'");
        }
    }

    [Fact]
    public void InternalUserMetadataDto_contains_only_allowed_metadata()
    {
        var props = typeof(InternalUserMetadataDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(x => x)
            .ToList();

        props.Should().Equal(new[]
        {
            "AccountStatus",
            "CareCircleCount",
            "CreatedAt",
            "DisabledAt",
            "DisabledReason",
            "DisplayName",
            "DocumentsCount",
            "Email",
            "IsDisabled",
            "StorageUsedBytes",
            "TimelineEntryCount",
            "UserId"
        }.OrderBy(x => x));
    }

    private static InternalUserMetadataService CreateService(AccantoDbContext db)
        => new((IAccantoDbContext)db);

    private static User SeedUser(AccantoDbContext db, string email = "user@example.com", string displayName = "User")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = displayName,
            PasswordHash = "x",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        return user;
    }

    [Fact]
    public async Task GetAsync_returns_only_metadata_and_aggregates()
    {
        using var db = TestDb.Create();
        var user = SeedUser(db);
        var circle = new CareCircle { Id = Guid.NewGuid(), Name = "Mamma", CreatedByUserId = user.Id, CreatedAt = DateTimeOffset.UtcNow };
        db.CareCircles.Add(circle);
        db.CareCircleMembers.Add(new CareCircleMember { Id = Guid.NewGuid(), CareCircleId = circle.Id, UserId = user.Id, Role = CareCircleRole.Owner, CreatedAt = DateTimeOffset.UtcNow });
        db.MedicalDocuments.Add(new MedicalDocument
        {
            Id = Guid.NewGuid(),
            CareCircleId = circle.Id,
            UploadedByUserId = user.Id,
            FileName = "abc123.pdf",
            OriginalFileName = "TAC_mamma_metastasi.pdf",
            ContentType = "application/pdf",
            SizeInBytes = 123456,
            Category = DocumentCategory.Imaging,
            StoragePath = "2026/07/abc123.pdf",
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.TimelineEntries.Add(new TimelineEntry
        {
            Id = Guid.NewGuid(),
            CareCircleId = circle.Id,
            CreatedByUserId = user.Id,
            OccurredAt = DateTimeOffset.UtcNow,
            Type = TimelineEntryType.PersonalNote,
            Title = "Papà ricovero",
            Content = "Ha smesso di mangiare",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var dto = await svc.GetAsync(user.Id);

        dto.Should().NotBeNull();
        dto!.UserId.Should().Be(user.Id);
        dto.Email.Should().Be("user@example.com");
        dto.DisplayName.Should().Be("User");
        dto.CareCircleCount.Should().Be(1);
        dto.DocumentsCount.Should().Be(1);
        dto.StorageUsedBytes.Should().Be(123456);
        dto.TimelineEntryCount.Should().Be(1);
        dto.AccountStatus.Should().Be("Active");

        // Privacy: il DTO serializzato NON deve contenere i valori sensibili.
        var json = System.Text.Json.JsonSerializer.Serialize(dto);
        json.Should().NotContain("Mamma");
        json.Should().NotContain("TAC_mamma_metastasi.pdf");
        json.Should().NotContain("2026/07/abc123.pdf");
        json.Should().NotContain("Papà ricovero");
        json.Should().NotContain("Ha smesso di mangiare");
    }

    [Fact]
    public async Task ListAsync_filters_and_paginates()
    {
        using var db = TestDb.Create();
        for (var i = 0; i < 5; i++)
            SeedUser(db, $"user{i}@example.com", $"User{i}");
        var disabled = SeedUser(db, "disabled@example.com", "Disabled");
        disabled.IsDisabled = true;
        await db.SaveChangesAsync();

        var svc = CreateService(db);

        var all = await svc.ListAsync(null, null, 1, 10);
        all.Total.Should().Be(6);
        all.Items.Should().HaveCount(6);

        var onlyDisabled = await svc.ListAsync(null, true, 1, 10);
        onlyDisabled.Total.Should().Be(1);
        onlyDisabled.Items[0].AccountStatus.Should().Be("Disabled");

        var search = await svc.ListAsync("user1@", null, 1, 10);
        search.Total.Should().Be(1);
        search.Items[0].Email.Should().Be("user1@example.com");

        var page = await svc.ListAsync(null, null, 2, 2);
        page.Items.Should().HaveCount(2);
        page.Page.Should().Be(2);
    }

    [Fact]
    public async Task AccountStatus_reflects_erased_and_disabled()
    {
        using var db = TestDb.Create();
        var erased = SeedUser(db, "erased@example.com");
        erased.IsErased = true;
        var disabled = SeedUser(db, "dis@example.com");
        disabled.IsDisabled = true;
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        (await svc.GetAsync(erased.Id))!.AccountStatus.Should().Be("Erased");
        (await svc.GetAsync(disabled.Id))!.AccountStatus.Should().Be("Disabled");
    }
}
