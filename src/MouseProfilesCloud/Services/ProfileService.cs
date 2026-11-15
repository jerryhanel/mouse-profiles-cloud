using System.Text.Json;
using MouseProfilesCloud.Models;
using MouseProfilesCloud.Repositories;

namespace MouseProfilesCloud.Services;

public class ProfileService
{
    private const int MaxNameLength = 100;
    private const int MaxConfigJsonBytes = 1_048_576; // 1 MB

    private readonly IProfileRepository _profiles;
    private readonly TimeProvider _time;

    public ProfileService(IProfileRepository profiles, TimeProvider time)
    {
        _profiles = profiles;
        _time = time;
    }

    public async Task<ServiceResult<IEnumerable<Profile>>> ListAsync(Guid userId)
    {
        var profiles = await _profiles.ListByUserAsync(userId);
        return ServiceResult.Ok(profiles);
    }

    public async Task<ServiceResult<Profile>> GetAsync(Guid userId, Guid profileId)
    {
        var profile = await _profiles.FindAsync(userId, profileId);
        if (profile == null)
            return ServiceResult.Fail<Profile>("Profile not found.", 404);

        return ServiceResult.Ok(profile);
    }

    public record SyncRequest(IEnumerable<SyncUpsert> Upsert, IEnumerable<Guid> Delete);
    public record SyncUpsert(Guid Id, string Name, string ConfigJson);
    public record SyncSummary(int Created, int Updated, int Deleted);

    public async Task<ServiceResult<SyncSummary>> SyncAsync(Guid userId, SyncRequest request)
    {
        var now = _time.GetUtcNow().UtcDateTime;

        var upserts = new List<Profile>();
        foreach (var u in request.Upsert)
        {
            if (u.Name.Length > MaxNameLength)
                return ServiceResult.Fail<SyncSummary>($"Profile name '{u.Name}' exceeds {MaxNameLength} characters.", 400);

            if (!IsValidJson(u.ConfigJson))
                return ServiceResult.Fail<SyncSummary>($"Profile '{u.Name}' contains invalid JSON.", 400);

            if (System.Text.Encoding.UTF8.GetByteCount(u.ConfigJson) > MaxConfigJsonBytes)
                return ServiceResult.Fail<SyncSummary>($"Profile '{u.Name}' config exceeds the 1 MB limit.", 400);

            upserts.Add(new Profile
            {
                Id = u.Id,
                UserId = userId,
                Name = u.Name,
                ConfigJson = u.ConfigJson,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        var existingIds = (await _profiles.ListByUserAsync(userId))
            .Select(p => p.Id)
            .ToHashSet();

        int created = upserts.Count(p => !existingIds.Contains(p.Id));
        int updated = upserts.Count(p => existingIds.Contains(p.Id));

        var deletesToRun = request.Delete.ToList();
        int deleted = deletesToRun.Count(id => existingIds.Contains(id));

        await _profiles.SyncAsync(userId, upserts, deletesToRun);

        return ServiceResult.Ok(new SyncSummary(created, updated, deleted));
    }

    private static bool IsValidJson(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
