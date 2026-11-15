using MouseProfilesCloud.Models;

namespace MouseProfilesCloud.Services;

public interface ITokenService
{
    string CreateToken(AppUser user);
}
