using Microsoft.Extensions.Time.Testing;
using Moq;
using MouseProfilesCloud.Models;
using MouseProfilesCloud.Repositories;
using MouseProfilesCloud.Services;

namespace MouseProfilesCloud.Tests.Profiles;

public class ProfileServiceTests
{
    private readonly Mock<IProfileRepository> _repo = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Guid _userId = Guid.NewGuid();

    private ProfileService CreateSut() => new(_repo.Object, _time);

    // ── List ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsProfilesForUser()
    {
        var profiles = new[] { new Profile { Id = Guid.NewGuid(), UserId = _userId, Name = "Gaming" } };
        _repo.Setup(r => r.ListByUserAsync(_userId)).ReturnsAsync(profiles);

        var result = await CreateSut().ListAsync(_userId);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!);
    }

    // ── Get ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_ExistingProfile_ReturnsIt()
    {
        var id = Guid.NewGuid();
        var profile = new Profile { Id = id, UserId = _userId, Name = "Work" };
        _repo.Setup(r => r.FindAsync(_userId, id)).ReturnsAsync(profile);

        var result = await CreateSut().GetAsync(_userId, id);

        Assert.True(result.Succeeded);
        Assert.Equal("Work", result.Value!.Name);
    }

    [Fact]
    public async Task Get_MissingProfile_Returns404()
    {
        _repo.Setup(r => r.FindAsync(_userId, It.IsAny<Guid>())).ReturnsAsync((Profile?)null);

        var result = await CreateSut().GetAsync(_userId, Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
    }

    // ── Sync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sync_NewProfiles_ReturnsCorrectCreatedCount()
    {
        _repo.Setup(r => r.ListByUserAsync(_userId)).ReturnsAsync(Array.Empty<Profile>());

        var upserts = new[]
        {
            new ProfileService.SyncUpsert(Guid.NewGuid(), "Gaming", "{}"),
            new ProfileService.SyncUpsert(Guid.NewGuid(), "Work", "{}"),
        };

        var result = await CreateSut().SyncAsync(_userId, new ProfileService.SyncRequest(upserts, []));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Created);
        Assert.Equal(0, result.Value.Updated);
    }

    [Fact]
    public async Task Sync_ExistingGuid_CountsAsUpdate()
    {
        var existingId = Guid.NewGuid();
        _repo.Setup(r => r.ListByUserAsync(_userId))
            .ReturnsAsync(new[] { new Profile { Id = existingId, UserId = _userId, Name = "Old" } });

        var upserts = new[] { new ProfileService.SyncUpsert(existingId, "Updated", "{}") };

        var result = await CreateSut().SyncAsync(_userId, new ProfileService.SyncRequest(upserts, []));

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Value!.Created);
        Assert.Equal(1, result.Value.Updated);
    }

    [Fact]
    public async Task Sync_DeletesExistingProfile_CountsOne()
    {
        var existingId = Guid.NewGuid();
        _repo.Setup(r => r.ListByUserAsync(_userId))
            .ReturnsAsync(new[] { new Profile { Id = existingId, UserId = _userId, Name = "Old" } });

        var result = await CreateSut().SyncAsync(_userId,
            new ProfileService.SyncRequest([], new[] { existingId }));

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.Deleted);
        _repo.Verify(r => r.SyncAsync(_userId, It.IsAny<IEnumerable<Profile>>(),
            It.Is<IEnumerable<Guid>>(ids => ids.Contains(existingId))), Times.Once);
    }

    [Fact]
    public async Task Sync_DeleteNonexistentProfile_CountsZero()
    {
        _repo.Setup(r => r.ListByUserAsync(_userId)).ReturnsAsync(Array.Empty<Profile>());
        var missingId = Guid.NewGuid();

        var result = await CreateSut().SyncAsync(_userId,
            new ProfileService.SyncRequest([], new[] { missingId }));

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Value!.Deleted);
    }

    [Fact]
    public async Task Sync_NameTooLong_Returns400()
    {
        _repo.Setup(r => r.ListByUserAsync(_userId)).ReturnsAsync(Array.Empty<Profile>());
        var longName = new string('x', 101);

        var result = await CreateSut().SyncAsync(_userId,
            new ProfileService.SyncRequest(
                [new ProfileService.SyncUpsert(Guid.NewGuid(), longName, "{}")], []));

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        _repo.Verify(r => r.SyncAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Profile>>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task Sync_InvalidConfigJson_Returns400()
    {
        _repo.Setup(r => r.ListByUserAsync(_userId)).ReturnsAsync(Array.Empty<Profile>());

        var result = await CreateSut().SyncAsync(_userId,
            new ProfileService.SyncRequest(
                [new ProfileService.SyncUpsert(Guid.NewGuid(), "Gaming", "not json")], []));

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Sync_SetsUpdatedAtFromTimeProvider()
    {
        var now = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        _time.SetUtcNow(now);
        _repo.Setup(r => r.ListByUserAsync(_userId)).ReturnsAsync(Array.Empty<Profile>());

        await CreateSut().SyncAsync(_userId,
            new ProfileService.SyncRequest(
                [new ProfileService.SyncUpsert(Guid.NewGuid(), "Gaming", "{}")], []));

        _repo.Verify(r => r.SyncAsync(_userId,
            It.Is<IEnumerable<Profile>>(ps => ps.All(p => p.UpdatedAt == now.UtcDateTime)),
            It.IsAny<IEnumerable<Guid>>()), Times.Once);
    }

    [Fact]
    public async Task Sync_AlwaysPassesCallerUserIdToRepository()
    {
        // The service never lets a caller substitute a different userId — the repo receives
        // the authenticated user's id regardless of what IDs appear in the request.
        _repo.Setup(r => r.ListByUserAsync(_userId)).ReturnsAsync(Array.Empty<Profile>());

        await CreateSut().SyncAsync(_userId,
            new ProfileService.SyncRequest([], new[] { Guid.NewGuid() }));

        _repo.Verify(r => r.SyncAsync(
            _userId,
            It.IsAny<IEnumerable<Profile>>(),
            It.IsAny<IEnumerable<Guid>>()), Times.Once);
    }

    [Fact]
    public async Task Sync_ConfigJsonOverSizeLimit_Returns400()
    {
        _repo.Setup(r => r.ListByUserAsync(_userId)).ReturnsAsync(Array.Empty<Profile>());
        var largeJson = $"{{\"data\":\"{new string('x', 1_048_577)}\"}}";

        var result = await CreateSut().SyncAsync(_userId,
            new ProfileService.SyncRequest(
                [new ProfileService.SyncUpsert(Guid.NewGuid(), "Big", largeJson)], []));

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }
}
