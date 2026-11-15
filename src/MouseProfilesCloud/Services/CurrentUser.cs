using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MouseProfilesCloud.Services;

public class CurrentUser : ICurrentUser
{
    public Guid UserId { get; }

    public CurrentUser(IHttpContextAccessor accessor)
    {
        var sub = accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? throw new InvalidOperationException("No authenticated user.");
        UserId = Guid.Parse(sub);
    }
}
