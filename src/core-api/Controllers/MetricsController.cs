using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NatureOS.CoreApi.Controllers;

[ApiController]
[Route("api/mycosoft/metrics")]
public class MetricsController : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Executive,Admin")] 
    public IActionResult GetMetrics()
    {
        return StatusCode(501, new
        {
            error = "metrics_unavailable",
            message = "Executive metrics require real data sources and are not configured."
        });
    }
}
