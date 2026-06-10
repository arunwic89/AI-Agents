using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var appPath = AppContext.BaseDirectory;
var rootPath = ResolveProjectRoot(appPath);
var configPath = Path.Combine(rootPath, "appsettings.json");

if (!File.Exists(configPath))
{
    Console.Error.WriteLine($"Missing configuration file: {configPath}");
    return;
}

var options = JsonSerializer.Deserialize<AppOptions>(File.ReadAllText(configPath), JsonOptions.CaseInsensitive)
    ?? throw new InvalidOperationException("Unable to parse appsettings.json");

using var tracerProvider = BuildTracerProvider(options.Tracing);

var httpClient = new HttpClient();
var modelClient = new FoundryModelClient(httpClient, options.Foundry, options.Agent.SystemInstruction);
var toolGateway = new ApimToolGateway(httpClient, options.Apim);
var agent = new OmniSupportAgent(modelClient, toolGateway);

Console.WriteLine("OmniSupport Agent started. Type 'exit' to quit.\n");

while (true)
{
    Console.Write("Customer> ");
    var input = Console.ReadLine();

    if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    if (string.IsNullOrWhiteSpace(input))
    {
        continue;
    }

    var response = await agent.HandleMessageAsync(input);
    Console.WriteLine($"Agent> {response}\n");
}

static TracerProvider? BuildTracerProvider(TracingOptions options)
{
    if (!options.Enabled)
    {
        return null;
    }

    if (!Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out var otlpUri))
    {
        Console.Error.WriteLine($"Invalid tracing endpoint: {options.OtlpEndpoint}");
        return null;
    }

    return Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(
            ResourceBuilder.CreateDefault().AddService(
                serviceName: options.ServiceName,
                serviceVersion: options.ServiceVersion))
        .AddSource(OmniTracing.SourceName)
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(exporterOptions =>
        {
            exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
            exporterOptions.Endpoint = otlpUri;
        })
        .Build();
}

static string ResolveProjectRoot(string appBaseDirectory)
{
    var dir = new DirectoryInfo(appBaseDirectory);

    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "appsettings.json");
        if (File.Exists(candidate))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    return appBaseDirectory;
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
}

internal static class OmniTracing
{
    public const string SourceName = "OmniSupport.Agent";
    public static readonly ActivitySource ActivitySource = new(SourceName);
}

internal sealed class OmniSupportAgent
{
    private readonly FoundryModelClient _modelClient;
    private readonly ApimToolGateway _toolGateway;

    public OmniSupportAgent(FoundryModelClient modelClient, ApimToolGateway toolGateway)
    {
        _modelClient = modelClient;
        _toolGateway = toolGateway;
    }

    public async Task<string> HandleMessageAsync(string userMessage)
    {
        using var activity = OmniTracing.ActivitySource.StartActivity("agent.handle_message");
        activity?.SetTag("agent.user_message.length", userMessage.Length);

        var decision = await _modelClient.GetDecisionAsync(userMessage);
        activity?.SetTag("agent.intent", decision.Intent);
        activity?.SetTag("agent.requires_tool", decision.RequiresTool);

        if (!decision.RequiresTool || string.IsNullOrWhiteSpace(decision.ToolName))
        {
            activity?.SetTag("agent.path", "no_tool");
            return decision.Reply;
        }

        activity?.SetTag("agent.path", "tool");
        activity?.SetTag("agent.tool_name", decision.ToolName);
        var toolResult = await _toolGateway.ExecuteAsync(decision.ToolName, decision.ToolInput);
        activity?.SetTag("agent.tool_success", toolResult.Success);
        return await _modelClient.ComposeFinalAnswerAsync(userMessage, decision, toolResult);
    }
}

internal sealed class FoundryModelClient
{
    private readonly HttpClient _httpClient;
    private readonly FoundryOptions _options;
    private readonly string _systemInstruction;

    public FoundryModelClient(HttpClient httpClient, FoundryOptions options, string systemInstruction)
    {
        _httpClient = httpClient;
        _options = options;
        _systemInstruction = systemInstruction;
    }

    public async Task<AgentDecision> GetDecisionAsync(string userMessage)
    {
        using var activity = OmniTracing.ActivitySource.StartActivity("model.get_decision");
        activity?.SetTag("model.deployment", _options.Deployment);

        var decisionPrompt = """
You are an orchestration engine for a customer support + sales + contract renewal assistant.
Return JSON only.

Allowed intents:
- mobile_repair
- printer_sales
- contract_renewal
- general_support

Allowed tool names:
- CreateServiceTicket
- GetContractDetails
- RenewContract
- None

Rules:
- Use tools only for account actions or system record actions.
- Ask concise follow-up questions when needed.
- If no tool is required, set requiresTool=false and toolName=None.

Output schema:
{
  "intent": "...",
  "reply": "...",
  "requiresTool": true,
  "toolName": "...",
  "toolInput": { }
}
""";

        var messages = new object[]
        {
            new { role = "system", content = _systemInstruction },
            new { role = "system", content = decisionPrompt },
            new { role = "user", content = userMessage }
        };

        var content = await CallModelAsync(messages, temperature: 0.1);

        try
        {
            var json = ExtractJson(content);
            var decision = JsonSerializer.Deserialize<AgentDecision>(json, JsonOptions.CaseInsensitive);

            if (decision is null)
            {
                throw new InvalidOperationException("Model returned empty decision payload.");
            }

            if (!IsAllowedTool(decision.ToolName))
            {
                decision.RequiresTool = false;
                decision.ToolName = "None";
            }

            if (string.IsNullOrWhiteSpace(decision.Reply))
            {
                decision.Reply = "I can help with repair requests, printer sales, or contract renewals. Could you share a bit more detail?";
            }

            activity?.SetTag("model.intent", decision.Intent);
            activity?.SetTag("model.requires_tool", decision.RequiresTool);
            activity?.SetTag("model.tool_name", decision.ToolName);

            return decision;
        }
        catch
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            return new AgentDecision
            {
                Intent = "general_support",
                Reply = "I can help with device repair, printer recommendations, or contract renewals. Could you share your request in one sentence?",
                RequiresTool = false,
                ToolName = "None",
                ToolInput = JsonDocument.Parse("{}").RootElement
            };
        }
    }

    public async Task<string> ComposeFinalAnswerAsync(string userMessage, AgentDecision decision, ToolExecutionResult toolResult)
    {
        using var activity = OmniTracing.ActivitySource.StartActivity("model.compose_final_answer");
        activity?.SetTag("model.deployment", _options.Deployment);
        activity?.SetTag("model.intent", decision.Intent);
        activity?.SetTag("model.tool_name", decision.ToolName);
        activity?.SetTag("model.tool_success", toolResult.Success);

        var prompt = $$"""
Given the customer message, orchestration decision, and tool execution result, compose a concise customer-facing response.

Customer message:
{{userMessage}}

Decision:
{{JsonSerializer.Serialize(decision)}}

Tool result:
{{JsonSerializer.Serialize(toolResult)}}

Rules:
- Keep response clear and actionable.
- If tool failed, acknowledge issue and provide next step.
- Do not expose internal error stack traces.
""";

        var messages = new object[]
        {
            new { role = "system", content = _systemInstruction },
            new { role = "user", content = prompt }
        };

        return await CallModelAsync(messages, temperature: 0.3);
    }

    private async Task<string> CallModelAsync(object[] messages, double temperature)
    {
        using var activity = OmniTracing.ActivitySource.StartActivity("model.chat_completion");
        activity?.SetTag("model.deployment", _options.Deployment);
        activity?.SetTag("model.temperature", temperature);

        var endpoint = BuildEndpoint();
        activity?.SetTag("model.endpoint", endpoint);
        var payload = new
        {
            model = _options.Deployment,
            messages,
            temperature,
            max_tokens = 600
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        request.Headers.Add("api-key", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        activity?.SetTag("http.status_code", (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            throw new InvalidOperationException($"Foundry call failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? string.Empty;
    }

    private string BuildEndpoint()
    {
        var endpoint = _options.Endpoint.TrimEnd('/');
        if (endpoint.Contains("?api-version=", StringComparison.OrdinalIgnoreCase))
        {
            return endpoint;
        }

        return $"{endpoint}?api-version={_options.ApiVersion}";
    }

    private static string ExtractJson(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException("No JSON object found in model output.");
        }

        return raw[start..(end + 1)];
    }

    private static bool IsAllowedTool(string? toolName)
    {
        return toolName is "CreateServiceTicket" or "GetContractDetails" or "RenewContract" or "None";
    }
}

internal sealed class ApimToolGateway
{
    private readonly HttpClient _httpClient;
    private readonly ApimOptions _options;

    public ApimToolGateway(HttpClient httpClient, ApimOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(string toolName, JsonElement toolInput)
    {
        using var activity = OmniTracing.ActivitySource.StartActivity("tool.execute");
        activity?.SetTag("tool.name", toolName);

        if (!_options.ToolRoutes.TryGetValue(toolName, out var route) || string.IsNullOrWhiteSpace(route))
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            return ToolExecutionResult.Fail(toolName, "Tool route is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.BaseUrl) || _options.BaseUrl.Contains("your-apim-name", StringComparison.OrdinalIgnoreCase))
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            return ToolExecutionResult.Fail(toolName, "APIM base URL is not configured.");
        }

        var url = CombineUrl(_options.BaseUrl, route);
        activity?.SetTag("tool.url", url);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(toolInput.GetRawText(), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_options.SubscriptionKey) &&
            !_options.SubscriptionKey.Contains("REPLACE_", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add("Ocp-Apim-Subscription-Key", _options.SubscriptionKey);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            activity?.SetTag("http.status_code", (int)response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                return ToolExecutionResult.Fail(toolName, $"Tool call failed with HTTP {(int)response.StatusCode}: {body}");
            }

            return ToolExecutionResult.Ok(toolName, body);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            return ToolExecutionResult.Fail(toolName, $"Tool call failed: {ex.Message}");
        }
    }

    private static string CombineUrl(string baseUrl, string route)
    {
        return $"{baseUrl.TrimEnd('/')}/{route.TrimStart('/')}";
    }
}

internal sealed class ToolExecutionResult
{
    public string ToolName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Payload { get; init; } = string.Empty;

    public static ToolExecutionResult Ok(string toolName, string payload)
    {
        return new ToolExecutionResult
        {
            ToolName = toolName,
            Success = true,
            Payload = payload
        };
    }

    public static ToolExecutionResult Fail(string toolName, string error)
    {
        return new ToolExecutionResult
        {
            ToolName = toolName,
            Success = false,
            Payload = error
        };
    }
}

internal sealed class AgentDecision
{
    public string Intent { get; set; } = "general_support";
    public string Reply { get; set; } = string.Empty;
    public bool RequiresTool { get; set; }
    public string ToolName { get; set; } = "None";
    public JsonElement ToolInput { get; set; } = JsonDocument.Parse("{}").RootElement;
}

internal sealed class AppOptions
{
    public FoundryOptions Foundry { get; set; } = new();
    public ApimOptions Apim { get; set; } = new();
    public AgentOptions Agent { get; set; } = new();
    public TracingOptions Tracing { get; set; } = new();
}

internal sealed class FoundryOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string Deployment { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2024-05-01-preview";
    public string ApiKey { get; set; } = string.Empty;
}

internal sealed class ApimOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string SubscriptionKey { get; set; } = string.Empty;
    public Dictionary<string, string> ToolRoutes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class AgentOptions
{
    public string SystemInstruction { get; set; } = string.Empty;
}

internal sealed class TracingOptions
{
    public bool Enabled { get; set; } = true;
    public string ServiceName { get; set; } = "omnisupport-agent";
    public string ServiceVersion { get; set; } = "1.0.0";
    public string OtlpEndpoint { get; set; } = "http://localhost:4318";
}
