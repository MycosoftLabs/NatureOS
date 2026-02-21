namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service for automated workflows
/// </summary>
public class WorkflowService : IWorkflowService
{
    private readonly ILogger<WorkflowService> _logger;
    private readonly List<WorkflowDefinition> _workflows = new();
    private readonly List<WorkflowExecution> _executions = new();
    private static readonly object _lock = new();

    public WorkflowService(ILogger<WorkflowService> logger)
    {
        _logger = logger;
    }

    public Task<IEnumerable<WorkflowDefinition>> GetWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock) return Task.FromResult(_workflows.AsEnumerable());
    }

    public Task<WorkflowDefinition?> GetWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        lock (_lock) return Task.FromResult(_workflows.FirstOrDefault(w => w.WorkflowId == workflowId));
    }

    public Task<WorkflowDefinition> SaveWorkflowAsync(WorkflowDefinition workflow, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(workflow.WorkflowId))
            workflow.WorkflowId = $"wf-{Guid.NewGuid():N}"[..12];
        lock (_lock)
        {
            var idx = _workflows.FindIndex(w => w.WorkflowId == workflow.WorkflowId);
            if (idx >= 0) _workflows[idx] = workflow;
            else _workflows.Add(workflow);
        }
        return Task.FromResult(workflow);
    }

    public Task<WorkflowExecutionResult> ExecuteWorkflowAsync(string workflowId, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        var execId = $"exec-{Guid.NewGuid():N}"[..16];
        lock (_lock)
        {
            _executions.Add(new WorkflowExecution
            {
                ExecutionId = execId,
                WorkflowId = workflowId,
                Status = "Completed",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });
        }
        return Task.FromResult(new WorkflowExecutionResult
        {
            ExecutionId = execId,
            Success = true,
            Message = "Workflow executed (stub)",
            CompletedAt = DateTime.UtcNow
        });
    }

    public Task<IEnumerable<WorkflowExecution>> GetExecutionHistoryAsync(string? workflowId = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var q = _executions.AsEnumerable();
            if (!string.IsNullOrEmpty(workflowId))
                q = q.Where(e => e.WorkflowId == workflowId);
            return Task.FromResult(q.OrderByDescending(e => e.StartedAt).Take(limit));
        }
    }
}
