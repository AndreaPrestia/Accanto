using Accanto.Application.Account;
using Accanto.Application.CareCircles;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Storage;
using Accanto.Domain.Entities;
using Accanto.Domain.Enums;
using Accanto.Infrastructure.Security;
using FluentAssertions;

namespace Accanto.Tests;

public class AccountServiceTests
{
    private sealed class FakeStorage : IFileStorage
    {
        public List<string> Deleted { get; } = new();
        public Task<StoredFile> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(string relativePath, CancellationToken ct = default) { Deleted.Add(relativePath); return Task.CompletedTask; }
    }

    private static (AccountService account, CareCircleService circles, Accanto.Infrastructure.Persistence.AccantoDbContext db, PasswordHasher hasher, FakeStorage storage) Build()
    {
        var db = TestDb.Create();
        var hasher = new PasswordHasher();
        var storage = new FakeStorage();
        var account = new AccountService(
            db, hasher, storage,
            new ChangePasswordRequestValidator(),
            new DeleteAccountRequestValidator());
        var auth = new Accanto.Infrastructure.Authorization.CareCircleAuthorization(db);
        var circles = new CareCircleService(db, auth, new NoOpAuditLog(), new CreateCareCircleRequestValidator(), new UpdateCareCircleRequestValidator());
        return (account, circles, db, hasher, storage);
    }

    private static async Task<Guid> SeedUser(Accanto.Infrastructure.Persistence.AccantoDbContext db, PasswordHasher hasher, string password = "password123")
    {
        var u = new User
        {
            Id = Guid.NewGuid(),
            Email = "u@test.local",
            DisplayName = "U",
            PasswordHash = hasher.Hash(password),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    [Fact]
    public async Task ChangePassword_updates_hash_when_current_password_is_correct()
    {
        var (account, _, db, hasher, _) = Build();
        var userId = await SeedUser(db, hasher, "oldpass12");

        await account.ChangePasswordAsync(userId, new ChangePasswordRequest("oldpass12", "newpass456"));

        var fresh = db.Users.Single(u => u.Id == userId);
        hasher.Verify("newpass456", fresh.PasswordHash).Should().BeTrue();
        hasher.Verify("oldpass12", fresh.PasswordHash).Should().BeFalse();
    }

    [Fact]
    public async Task ChangePassword_rejects_wrong_current_password()
    {
        var (account, _, db, hasher, _) = Build();
        var userId = await SeedUser(db, hasher, "oldpass12");

        var act = async () => await account.ChangePasswordAsync(userId, new ChangePasswordRequest("wrongpass", "newpass456"));
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ChangePassword_rejects_short_new_password()
    {
        var (account, _, db, hasher, _) = Build();
        var userId = await SeedUser(db, hasher, "oldpass12");

        var act = async () => await account.ChangePasswordAsync(userId, new ChangePasswordRequest("oldpass12", "short"));
        await act.Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public async Task ChangePassword_rejects_same_new_password()
    {
        var (account, _, db, hasher, _) = Build();
        var userId = await SeedUser(db, hasher, "oldpass12");

        var act = async () => await account.ChangePasswordAsync(userId, new ChangePasswordRequest("oldpass12", "oldpass12"));
        await act.Should().ThrowAsync<AppValidationException>();
    }

    [Fact]
    public async Task DeleteAccount_rejects_wrong_password()
    {
        var (account, _, db, hasher, _) = Build();
        var userId = await SeedUser(db, hasher, "rightpass");

        var act = async () => await account.DeleteAsync(userId, new DeleteAccountRequest("wrongpass"));
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task DeleteAccount_refuses_when_user_shares_a_circle_with_others()
    {
        var (account, circles, db, hasher, _) = Build();
        var userId = await SeedUser(db, hasher);

        var circle = await circles.CreateAsync(userId, new CreateCareCircleRequest("Mamma", null));
        db.CareCircleMembers.Add(new CareCircleMember
        {
            Id = Guid.NewGuid(),
            CareCircleId = circle.Id,
            UserId = Guid.NewGuid(),
            Role = CareCircleRole.Caregiver,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var act = async () => await account.DeleteAsync(userId, new DeleteAccountRequest("password123"));
        await act.Should().ThrowAsync<ConflictException>();

        db.Users.Any(u => u.Id == userId).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAccount_cascades_solo_circles_and_removes_user()
    {
        var (account, circles, db, hasher, storage) = Build();
        var userId = await SeedUser(db, hasher);

        var circle = await circles.CreateAsync(userId, new CreateCareCircleRequest("Mamma", null));

        db.TimelineEntries.Add(new TimelineEntry
        {
            Id = Guid.NewGuid(),
            CareCircleId = circle.Id,
            CreatedByUserId = userId,
            Type = TimelineEntryType.PersonalNote,
            Title = "n",
            OccurredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.MedicalDocuments.Add(new MedicalDocument
        {
            Id = Guid.NewGuid(),
            CareCircleId = circle.Id,
            UploadedByUserId = userId,
            FileName = "x.pdf",
            ContentType = "application/pdf",
            SizeInBytes = 10,
            StoragePath = "blob/x.pdf",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        await account.DeleteAsync(userId, new DeleteAccountRequest("password123"));

        db.Users.Any(u => u.Id == userId).Should().BeFalse();
        db.CareCircles.Any(c => c.Id == circle.Id).Should().BeFalse();
        db.TimelineEntries.Any(t => t.CareCircleId == circle.Id).Should().BeFalse();
        db.MedicalDocuments.Any(d => d.CareCircleId == circle.Id).Should().BeFalse();
        storage.Deleted.Should().Contain("blob/x.pdf");
    }
}
