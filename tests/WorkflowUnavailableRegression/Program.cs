global using Microsoft.Extensions.Logging;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NatureOS.CoreApi.Controllers;
using NatureOS.CoreApi.Services;

// No application startup or HTTP listener. The real event service receives an
// HttpClient whose handler records attempts instead of making network calls.
Environment.SetEnvironmentVariable("MYCA_DEEP_AGENTS_DOMAIN_HOOKS_ENABLED", "true");
Environment.SetEnvironmentVariable("MAS_API_URL", "http://offline-fixture.invalid");

var tests = new (string Name, Func<Task> Run)[]
{
    ("Known workflow reports unsupported without successful execution", KnownIsUnsupported),
    ("Unknown workflow also cannot fabricate execution", UnknownIsUnsupported),
    ("Unsupported execution creates no ID, timestamp or history", NoFabricatedExecution),
    ("Completion timestamp serializes as null", NullCompletionJson),
    ("Known workflow controller returns 501 and no event", KnownController),
    ("Unknown workflow controller returns 501 and no event", UnknownController),
    ("Controller publishes no event for unsupported known workflow", KnownNoEvent),
    ("Controller publishes no event for unsupported unknown workflow", UnknownNoEvent),
    ("Definition CRUD still works within one service instance", DefinitionCrud),
    ("Repeated unsupported attempts never manufacture history", RepeatedAttempts),
    ("Unavailable warning excludes user-controlled log separators", NoUserControlledLogMessage),
    ("Unavailable warning excludes request data from structured state", NoUserControlledLogState),
};

var failed = 0;
foreach (var (name, run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.WriteLine($"FAIL: {name}: {exception.Message}");
    }
}
Console.WriteLine($"RESULT: {tests.Length - failed} passed, {failed} failed; {tests.Length} total");
return failed == 0 ? 0 : 1;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static WorkflowService Service() => new(NullLogger<WorkflowService>.Instance);

static async Task<WorkflowDefinition> Save(WorkflowService service)
{
    return await service.SaveWorkflowAsync(new WorkflowDefinition
    {
        Name = "Offline fixture",
        Steps = new List<WorkflowStep>
        {
            new() { StepId = "fixture-step", Action = "device.command" }
        }
    });
}

static async Task KnownIsUnsupported()
{
    var service = Service();
    var definition = await Save(service);
    var result = await service.ExecuteWorkflowAsync(definition.WorkflowId);
    Check(!result.Success, "Success must be false without an executor");
    Check(result.Message?.Contains("unavailable", StringComparison.OrdinalIgnoreCase) == true,
        "Result must explain that execution is unavailable");
}

static async Task UnknownIsUnsupported()
{
    var result = await Service().ExecuteWorkflowAsync("missing-workflow");
    Check(!result.Success, "Missing workflow must never be reported executed");
}

static async Task NoFabricatedExecution()
{
    var service = Service();
    var definition = await Save(service);
    var result = await service.ExecuteWorkflowAsync(definition.WorkflowId);
    Check(string.IsNullOrEmpty(result.ExecutionId), "No execution ID may be invented");
    Check((object?)result.CompletedAt is null, "No completion timestamp may be invented");
    Check(!(await service.GetExecutionHistoryAsync()).Any(), "History must remain empty");
}

static async Task NullCompletionJson()
{
    var result = await Service().ExecuteWorkflowAsync("missing-workflow");
    using var json = JsonDocument.Parse(JsonSerializer.Serialize(result,
        new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    Check(json.RootElement.GetProperty("completedAt").ValueKind == JsonValueKind.Null,
        "HTTP JSON completion timestamp must be null, not now or DateTime.MinValue");
    Check(!json.RootElement.GetProperty("success").GetBoolean(), "JSON must not claim success");
}

static async Task<(ActionResult<WorkflowExecutionResult> Result, RecordingFactory Http, WorkflowService Service)> ControllerAttempt(bool known)
{
    var service = Service();
    var id = known ? (await Save(service)).WorkflowId : "missing-workflow";
    var http = new RecordingFactory();
    var events = new DeepAgentEventService(http, NullLogger<DeepAgentEventService>.Instance);
    var controller = new WorkflowController(service, events, NullLogger<WorkflowController>.Instance);
    var result = await controller.ExecuteWorkflow(id, new Dictionary<string, object>
    {
        ["action"] = "device.command",
        ["target"] = "http://offline-fixture.invalid/no-dispatch"
    });
    return (result, http, service);
}

static async Task KnownController() => await AssertController(true);
static async Task UnknownController() => await AssertController(false);

static async Task AssertController(bool known)
{
    var (response, http, service) = await ControllerAttempt(known);
    Check(response.Result is ObjectResult { StatusCode: 501 }, "HTTP result must be 501, not 2xx");
    var body = ((ObjectResult)response.Result!).Value as WorkflowExecutionResult;
    Check(body is { Success: false }, "HTTP response body must explicitly reject success");
    Check((object?)body!.CompletedAt is null, "HTTP body must not claim completion");
    Check(http.Created == 0 && http.Handler.Requests == 0, "No event HTTP client or request may be created");
    Check(!(await service.GetExecutionHistoryAsync()).Any(), "Controller attempt must not create history");
}

static async Task KnownNoEvent() => await AssertNoEvent(true);
static async Task UnknownNoEvent() => await AssertNoEvent(false);

static async Task AssertNoEvent(bool known)
{
    var (_, http, _) = await ControllerAttempt(known);
    Check(http.Created == 0 && http.Handler.Requests == 0,
        $"Unsupported attempt published an event: clients={http.Created}, HTTP requests={http.Handler.Requests}");
}

static async Task DefinitionCrud()
{
    var service = Service();
    var definition = await Save(service);
    Check(!string.IsNullOrEmpty(definition.WorkflowId), "Definition still receives an ID");
    Check((await service.GetWorkflowsAsync()).Count() == 1, "Definition list remains available");
    definition.Name = "Updated offline fixture";
    await service.SaveWorkflowAsync(definition);
    Check((await service.GetWorkflowAsync(definition.WorkflowId))?.Name == definition.Name,
        "Definition update remains available within the same service instance");
    Check(await service.GetWorkflowAsync("missing") is null, "Unknown definition remains missing");
}

static async Task RepeatedAttempts()
{
    var service = Service();
    var definition = await Save(service);
    for (var attempt = 0; attempt < 3; attempt++)
        await service.ExecuteWorkflowAsync(definition.WorkflowId);
    Check(!(await service.GetExecutionHistoryAsync(definition.WorkflowId)).Any(),
        "Repeated unavailable requests must never fabricate completed history");
}

static async Task NoUserControlledLogMessage()
{
    var logger = new RecordingLogger<WorkflowService>();
    var service = new WorkflowService(logger);
    var id = "untrusted-workflow\r\nforged-record\u001b[31m\u2028\u2029";
    var result = await service.ExecuteWorkflowAsync(id);
    Check(!result.Success && result.CompletedAt is null,
        "Unsafe identifier must not change the unsupported execution result");
    Check(logger.Entries.Count == 1 && logger.Entries[0].Level == LogLevel.Warning,
        "Unavailable execution must still emit one useful warning");
    var message = logger.Entries[0].Message;
    Check(message.Contains("unavailable", StringComparison.OrdinalIgnoreCase),
        "Warning must retain the operational reason");
    Check(!message.Contains(id, StringComparison.Ordinal)
          && message.IndexOfAny(new[] { '\r', '\n', '\u001b', '\u2028', '\u2029' }) < 0,
        "User-controlled workflow ID or log separators leaked into the rendered warning");
}

static async Task NoUserControlledLogState()
{
    const string id = "private-workflow-fixture";
    const string parameter = "private-parameter-fixture";
    var logger = new RecordingLogger<WorkflowService>();
    await new WorkflowService(logger).ExecuteWorkflowAsync(id,
        new Dictionary<string, object> { ["fixture"] = parameter });
    Check(logger.Entries.Count == 1, "Expected one warning");
    var entry = logger.Entries[0];
    Check(!entry.Message.Contains(id, StringComparison.Ordinal)
          && !entry.Message.Contains(parameter, StringComparison.Ordinal),
        "Request data leaked into the warning text");
    var state = entry.State as IEnumerable<KeyValuePair<string, object?>>
        ?? throw new InvalidOperationException("Expected structured logger state");
    var fields = state.ToArray();
    Check(fields.Length == 1
          && fields[0].Key == "{OriginalFormat}"
          && fields[0].Value is string format
          && string.Equals(format, entry.Message, StringComparison.Ordinal),
        "Warning state must contain only its fixed message template, with no request fields or nested values");
}

sealed class RecordingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, object? State)> Entries { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception), state));
}

sealed class RecordingFactory : IHttpClientFactory
{
    public RecordingHandler Handler { get; } = new();
    public int Created { get; private set; }
    public HttpClient CreateClient(string name)
    {
        Created++;
        return new HttpClient(Handler, disposeHandler: false);
    }
}

sealed class RecordingHandler : HttpMessageHandler
{
    public int Requests { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Requests++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
    }
}
