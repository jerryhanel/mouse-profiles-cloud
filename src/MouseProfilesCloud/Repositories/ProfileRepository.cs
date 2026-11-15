using Dapper;
using MouseProfilesCloud.Data;
using MouseProfilesCloud.Models;

namespace MouseProfilesCloud.Repositories;

public class ProfileRepository : IProfileRepository
{
    private readonly Db _db;

    public ProfileRepository(Db db) => _db = db;

    public async Task<IEnumerable<Profile>> ListByUserAsync(Guid userId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Profile>(
            "SELECT Id, UserId, Name, CreatedAt, UpdatedAt FROM Profiles WHERE UserId = @userId",
            new { userId });
    }

    public async Task<Profile?> FindAsync(Guid userId, Guid profileId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Profile>(
            "SELECT * FROM Profiles WHERE UserId = @userId AND Id = @profileId",
            new { userId, profileId });
    }

    public async Task SyncAsync(Guid userId, IEnumerable<Profile> upserts, IEnumerable<Guid> deletes)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        foreach (var p in upserts)
        {
            await conn.ExecuteAsync(
                @"INSERT INTO Profiles (Id, UserId, Name, ConfigJson, CreatedAt, UpdatedAt)
                  VALUES (@Id, @UserId, @Name, @ConfigJson, @CreatedAt, @UpdatedAt)
                  ON DUPLICATE KEY UPDATE
                    Name = IF(UserId = @UserId, @Name, Name),
                    ConfigJson = IF(UserId = @UserId, @ConfigJson, ConfigJson),
                    UpdatedAt = IF(UserId = @UserId, @UpdatedAt, UpdatedAt)",
                p, tx);
        }

        foreach (var id in deletes)
        {
            await conn.ExecuteAsync(
                "DELETE FROM Profiles WHERE UserId = @userId AND Id = @id",
                new { userId, id }, tx);
        }

        await tx.CommitAsync();
    }
}
