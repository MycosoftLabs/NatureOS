namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service for safe command execution (restricted allowlist)
/// </summary>
public class ShellService : IShellService
{
    private readonly ILogger<ShellService> _logger;
    private static readonly HashSet<string> AllowedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "echo", "date", "whoami", "pwd", "ls", "dir", "help", "version", "status"
    };

    public ShellService(ILogger<ShellService> logger)
    {
        _logger = logger;
    }

    public Task<ShellExecutionResult> ExecuteAsync(string command, string[]? args = null, CancellationToken cancellationToken = default)
    {
        var cmd = command.Trim().Split(' ')[0];
        if (!IsCommandAllowed(cmd))
        {
            return Task.FromResult(new ShellExecutionResult
            {
                Success = false,
                Error = $"Command '{cmd}' is not in the allowed list. Allowed: {string.Join(", ", GetAllowedCommands())}",
                ExitCode = 1,
                ExecutedAt = DateTime.UtcNow
            });
        }

        return Task.FromResult(new ShellExecutionResult
        {
            Success = true,
            Output = $"Executed: {command} (simulated - production would run safe commands)",
            ExitCode = 0,
            ExecutedAt = DateTime.UtcNow
        });
    }

    public IEnumerable<string> GetAllowedCommands() => AllowedCommands;

    public bool IsCommandAllowed(string command) => AllowedCommands.Contains(command.Trim().Split(' ')[0]);
}
