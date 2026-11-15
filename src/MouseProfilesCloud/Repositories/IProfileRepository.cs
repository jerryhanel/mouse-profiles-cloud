using MouseProfilesCloud.Models;

namespace MouseProfilesCloud.Repositories;

public interface IProfileRepository
{
    Task<IEnumerable<Profile>> ListByUserAsync(Guid userId);
    Task<Profile?> FindAsync(Guid userId, Guid profileId);
    Task SyncAsync(Guid userId, IEnumerable<Profile> upserts, IEnumerable<Guid> deletes);
}
