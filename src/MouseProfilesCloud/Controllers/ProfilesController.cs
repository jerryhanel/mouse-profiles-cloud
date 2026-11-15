using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MouseProfilesCloud.Services;
using static MouseProfilesCloud.Services.ProfileService;

namespace MouseProfilesCloud.Controllers;

[ApiController]
[Route("profiles")]
[Authorize]
public class ProfilesController : ControllerBase
{
    private readonly ProfileService _profiles;
    private readonly ICurrentUser _currentUser;

    public ProfilesController(ProfileService profiles, ICurrentUser currentUser)
    {
        _profiles = profiles;
        _currentUser = currentUser;
    }

    public record ProfileMetaResponse(Guid Id, string Name);
    public record ProfileDetailResponse(Guid Id, string Name, string ConfigJson);

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var result = await _profiles.ListAsync(_currentUser.UserId);
        return Ok(result.Value!.Select(p => new ProfileMetaResponse(p.Id, p.Name)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _profiles.GetAsync(_currentUser.UserId, id);
        if (!result.Succeeded)
            return Problem(result.Error, statusCode: result.StatusCode);

        var p = result.Value!;
        return Ok(new ProfileDetailResponse(p.Id, p.Name, p.ConfigJson!));
    }

    public record SyncRequestDto(
        [Required] IEnumerable<SyncUpsert> Upsert,
        [Required] IEnumerable<Guid> Delete);

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(SyncRequestDto req)
    {
        var result = await _profiles.SyncAsync(
            _currentUser.UserId,
            new SyncRequest(req.Upsert, req.Delete));

        if (!result.Succeeded)
            return Problem(result.Error, statusCode: result.StatusCode);

        return Ok(result.Value);
    }
}
