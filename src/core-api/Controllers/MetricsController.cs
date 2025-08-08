using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NatureOS.CoreApi.Controllers;

[ApiController]
[Route("api/mycosoft/metrics")]
public class MetricsController : ControllerBase
{
    // Minimal placeholder; in production, enforce RBAC for executive roles
    [HttpGet]
    [Authorize(Roles = "Executive,Admin")] 
    public IActionResult GetMetrics()
    {
        var now = DateTime.UtcNow;
        return Ok(new {
            timestamp = now,
            finance = new { burnRate = 0.65, runwayMonths = 14 },
            hr = new { teamSize = 7, openRoles = 2 },
            product = new { activeUsers = 1234, dailyIngestion = 2300 }
        });
    }
}
