namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service interface for command execution and scripting
/// </summary>
public interface IShellService
{
    /// <summary>
    /// Execute a shell command (restricted safe commands only)
    /// </summary>
    Task<ShellExecutionResult> ExecuteAsync(string command, string[]? args = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get list of allowed commands
    /// </summary>
    IEnumerable<string> GetAllowedCommands();

    /// <summary>
    /// Validate if a command is allowed
    /// </summary>
    bool IsCommandAllowed(string command);
}

/// <summary>
/// Shell command execution result
/// </summary>
public class ShellExecutionResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string? Error { get; set; }
    public int ExitCode { get; set; }
    public DateTime ExecutedAt { get; set; }
}
