namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service interface for automated workflows
/// </summary>
public interface IWorkflowService
{
    /// <summary>
    /// Get all workflows
    /// </summary>
    Task<IEnumerable<WorkflowDefinition>> GetWorkflowsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get workflow by ID
    /// </summary>
    Task<WorkflowDefinition?> GetWorkflowAsync(string workflowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create or update a workflow
    /// </summary>
    Task<WorkflowDefinition> SaveWorkflowAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trigger workflow execution
    /// </summary>
    Task<WorkflowExecutionResult> ExecuteWorkflowAsync(string workflowId, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get workflow execution history
    /// </summary>
    Task<IEnumerable<WorkflowExecution>> GetExecutionHistoryAsync(string? workflowId = null, int limit = 50, CancellationToken cancellationToken = default);
}

/// <summary>
/// Workflow definition
/// </summary>
public class WorkflowDefinition
{
    public string WorkflowId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Draft";
    public List<WorkflowStep> Steps { get; set; } = new();
}

/// <summary>
/// Workflow step
/// </summary>
public class WorkflowStep
{
    public string StepId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Workflow execution result
/// </summary>
public class WorkflowExecutionResult
{
    public string ExecutionId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DateTime CompletedAt { get; set; }
}

/// <summary>
/// Workflow execution record
/// </summary>
public class WorkflowExecution
{
    public string ExecutionId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
