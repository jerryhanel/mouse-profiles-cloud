using Dapper;
using MouseProfilesCloud.Data;
using MouseProfilesCloud.Models;

namespace MouseProfilesCloud.Repositories;

public class UserRepository : IUserRepository
{
    private readonly Db _db;

    public UserRepository(Db db) => _db = db;

    public async Task<AppUser?> FindByEmailAsync(string email)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<AppUser>(
            "SELECT * FROM Users WHERE Email = @email", new { email });
    }

    public async Task<AppUser?> FindByVerifyTokenAsync(string token)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<AppUser>(
            "SELECT * FROM Users WHERE VerifyToken = @token AND VerifyTokenExpiry > UTC_TIMESTAMP()",
            new { token });
    }

    public async Task CreateAsync(AppUser user)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            @"INSERT INTO Users (Id, Email, PasswordHash, EmailVerified, VerifyToken, VerifyTokenExpiry, CreatedAt)
              VALUES (@Id, @Email, @PasswordHash, @EmailVerified, @VerifyToken, @VerifyTokenExpiry, @CreatedAt)",
            user);
    }

    public async Task MarkEmailVerifiedAsync(Guid userId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Users SET EmailVerified = 1, VerifyToken = NULL, VerifyTokenExpiry = NULL WHERE Id = @userId",
            new { userId });
    }

    public async Task SweepUnverifiedAsync()
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "DELETE FROM Users WHERE EmailVerified = 0 AND CreatedAt < UTC_TIMESTAMP() - INTERVAL 5 DAY");
    }

    public async Task UpdateVerifyTokenAsync(Guid userId, string token, DateTime expiry)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Users SET VerifyToken = @token, VerifyTokenExpiry = @expiry WHERE Id = @userId",
            new { userId, token, expiry });
    }
}
