using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NatureOS.CoreApi.Controllers;

[ApiController]
[Route("api/security")] 
public class SecurityController : ControllerBase
{
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new {
            User = User.Identity?.Name ?? "anonymous",
            Roles = User.Claims.Where(c => c.Type.EndsWith("/role")).Select(c => c.Value).ToArray()
        });
    }
}
