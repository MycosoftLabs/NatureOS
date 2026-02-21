using Microsoft.AspNetCore.Mvc;
using NatureOS.CoreApi.Services;

namespace NatureOS.CoreApi.Controllers;

/// <summary>
/// API controller for command execution and scripting (restricted safe commands only)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ShellController : ControllerBase
{
    private readonly IShellService _shellService;
    private readonly ILogger<ShellController> _logger;

    public ShellController(IShellService shellService, ILogger<ShellController> logger)
    {
        _shellService = shellService;
        _logger = logger;
    }

    /// <summary>
    /// Execute a shell command (restricted allowlist)
    /// </summary>
    [HttpPost("execute")]
    [ProducesResponseType(typeof(ShellExecutionResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ShellExecutionResult>> Execute(
        [FromBody] ShellExecuteRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Command))
                return BadRequest("Command is required");

            var result = await _shellService.ExecuteAsync(request.Command, request.Args, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get list of allowed commands
    /// </summary>
    [HttpGet("commands")]
    [ProducesResponseType(typeof(IEnumerable<string>), 200)]
    public ActionResult<IEnumerable<string>> GetAllowedCommands()
    {
        return Ok(_shellService.GetAllowedCommands());
    }
}

/// <summary>
/// Shell execute request
/// </summary>
public class ShellExecuteRequest
{
    public string Command { get; set; } = string.Empty;
    public string[]? Args { get; set; }
}
