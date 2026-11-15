using MouseProfilesCloud.Models;

namespace MouseProfilesCloud.Repositories;

public interface IUserRepository
{
    Task<AppUser?> FindByEmailAsync(string email);
    Task<AppUser?> FindByVerifyTokenAsync(string token);
    Task CreateAsync(AppUser user);
    Task MarkEmailVerifiedAsync(Guid userId);
    Task SweepUnverifiedAsync();
    Task UpdateVerifyTokenAsync(Guid userId, string token, DateTime expiry);
}
