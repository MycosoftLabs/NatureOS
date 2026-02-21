using Microsoft.AspNetCore.Mvc;
using NatureOS.CoreApi.Services;

namespace NatureOS.CoreApi.Controllers;

/// <summary>
/// API controller for automated workflows
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowService _workflowService;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(IWorkflowService workflowService, ILogger<WorkflowController> logger)
    {
        _workflowService = workflowService;
        _logger = logger;
    }

    /// <summary>
    /// Get all workflows
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<WorkflowDefinition>), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<WorkflowDefinition>>> GetWorkflows(CancellationToken cancellationToken = default)
    {
        try
        {
            var workflows = await _workflowService.GetWorkflowsAsync(cancellationToken);
            return Ok(workflows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get workflows");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get workflow by ID
    /// </summary>
    [HttpGet("{workflowId}")]
    [ProducesResponseType(typeof(WorkflowDefinition), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<WorkflowDefinition>> GetWorkflow(
        string workflowId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var workflow = await _workflowService.GetWorkflowAsync(workflowId, cancellationToken);
            if (workflow == null)
                return NotFound($"Workflow {workflowId} not found");
            return Ok(workflow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get workflow {WorkflowId}", workflowId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Create or update a workflow
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(WorkflowDefinition), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<WorkflowDefinition>> SaveWorkflow(
        [FromBody] WorkflowDefinition workflow,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(workflow.Name))
                return BadRequest("Workflow name is required");

            var saved = await _workflowService.SaveWorkflowAsync(workflow, cancellationToken);
            return Ok(saved);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save workflow");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Execute a workflow
    /// </summary>
    [HttpPost("{workflowId}/execute")]
    [ProducesResponseType(typeof(WorkflowExecutionResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<WorkflowExecutionResult>> ExecuteWorkflow(
        string workflowId,
        [FromBody] Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _workflowService.ExecuteWorkflowAsync(workflowId, parameters, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute workflow {WorkflowId}", workflowId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get workflow execution history
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(IEnumerable<WorkflowExecution>), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<WorkflowExecution>>> GetExecutionHistory(
        [FromQuery] string? workflowId = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var history = await _workflowService.GetExecutionHistoryAsync(workflowId, limit, cancellationToken);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get execution history");
            return StatusCode(500, "Internal server error");
        }
    }
}
