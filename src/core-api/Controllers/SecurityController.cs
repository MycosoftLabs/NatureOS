using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NatureOS.CoreApi.Services;

namespace NatureOS.CoreApi.Controllers;

[ApiController]
[Route("api/security")] 
public class SecurityController : ControllerBase
{
    private readonly DeepAgentEventService _deepAgentEvents;

    public SecurityController(DeepAgentEventService deepAgentEvents)
    {
        _deepAgentEvents = deepAgentEvents;
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken = default)
    {
        await _deepAgentEvents.PublishAsync(
            domain: "security",
            task: "NatureOS security profile requested",
            context: new { route = "/api/security/me" },
            preferredAgent: "security-agent",
            cancellationToken: cancellationToken);
        return Ok(new {
            User = User.Identity?.Name ?? "anonymous",
            Roles = User.Claims.Where(c => c.Type.EndsWith("/role")).Select(c => c.Value).ToArray()
        });
    }
}
