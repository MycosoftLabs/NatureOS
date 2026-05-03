using System.Text;
using System.Text.Json;

namespace NatureOS.CoreApi.Services;

public sealed class DeepAgentEventService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DeepAgentEventService> _logger;

    public DeepAgentEventService(
        IHttpClientFactory httpClientFactory,
        ILogger<DeepAgentEventService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task PublishAsync(
        string domain,
        string task,
        object? context = null,
        string? preferredAgent = null,
        CancellationToken cancellationToken = default)
    {
        var hooksEnabled = Environment.GetEnvironmentVariable("MYCA_DEEP_AGENTS_DOMAIN_HOOKS_ENABLED");
        if (string.Equals(hooksEnabled, "false", StringComparison.OrdinalIgnoreCase))
            return;

        var masBase = (Environment.GetEnvironmentVariable("MAS_API_URL") ?? "http://192.168.0.188:8001").TrimEnd('/');
        var payload = new
        {
            domain,
            task,
            context = context ?? new { },
            preferred_agent = preferredAgent,
        };

        try
        {
            using var client = _httpClientFactory.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{masBase}/api/deep-agents/domain-event")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"),
            };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            _ = await client.SendAsync(req, cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Deep Agent event publish failed for domain {Domain}", domain);
        }
    }
}
