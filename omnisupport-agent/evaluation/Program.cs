using System.Text;
using System.Text.Json;

var appBase = AppContext.BaseDirectory;
var evalRoot = ResolveProjectRoot(appBase, "appsettings.eval.json");
var agentRoot = ResolveProjectRoot(appBase, "appsettings.json");

var evalConfig = LoadJson<EvalConfig>(Path.Combine(evalRoot, "appsettings.eval.json"));
var appOptions = LoadJson<AppOptions>(Path.Combine(agentRoot, "appsettings.json"));

var datasetPath = Path.GetFullPath(Path.Combine(evalRoot, evalConfig.DatasetPath));
var outputDir = Path.GetFullPath(Path.Combine(evalRoot, evalConfig.OutputDirectory));
Directory.CreateDirectory(outputDir);

var testCases = LoadJson<List<EvalCase>>(datasetPath);
if (testCases.Count == 0)
{
    Console.Error.WriteLine("No evaluation test cases found.");
    return;
}

using var httpClient = new HttpClient();
var evaluator = new ModelDecisionEvaluator(httpClient, appOptions.Foundry, appOptions.Agent.SystemInstruction, evalConfig);

var results = new List<CaseResult>();

foreach (var test in testCases)
{
    var prediction = await evaluator.PredictAsync(test.Query);

    var intentMatch = string.Equals(test.ExpectedIntent, prediction.Intent, StringComparison.OrdinalIgnoreCase);
    var toolMatch = string.Equals(test.ExpectedTool, prediction.ToolName, StringComparison.OrdinalIgnoreCase);

    results.Add(new CaseResult
    {
        Id = test.Id,
        Query = test.Query,
        ExpectedIntent = test.ExpectedIntent,
        PredictedIntent = prediction.Intent,
        IntentMatch = intentMatch,
        ExpectedTool = test.ExpectedTool,
        PredictedTool = prediction.ToolName,
        ToolMatch = toolMatch,
        RawReply = prediction.Reply
    });

    Console.WriteLine($"[{test.Id}] intent={prediction.Intent} tool={prediction.ToolName} intentMatch={intentMatch} toolMatch={toolMatch}");
}

var intentAccuracy = results.Count(r => r.IntentMatch) / (double)results.Count;
var toolAccuracy = results.Count(r => r.ToolMatch) / (double)results.Count;

var report = new EvalReport
{
    TimestampUtc = DateTime.UtcNow,
    TotalCases = results.Count,
    IntentAccuracy = intentAccuracy,
    ToolAccuracy = toolAccuracy,
    IntentTarget = evalConfig.Metrics.IntentAccuracyTarget,
    ToolTarget = evalConfig.Metrics.ToolAccuracyTarget,
    PassedIntentTarget = intentAccuracy >= evalConfig.Metrics.IntentAccuracyTarget,
    PassedToolTarget = toolAccuracy >= evalConfig.Metrics.ToolAccuracyTarget,
    Cases = results
};

var reportJson = JsonSerializer.Serialize(report, JsonDefaults.WriteIndented);
var reportPath = Path.Combine(outputDir, "latest-report.json");
await File.WriteAllTextAsync(reportPath, reportJson);

var csvPath = Path.Combine(outputDir, "latest-report.csv");
await File.WriteAllTextAsync(csvPath, BuildCsv(results));

Console.WriteLine();
Console.WriteLine("Evaluation complete.");
Console.WriteLine($"Total Cases      : {report.TotalCases}");
Console.WriteLine($"Intent Accuracy  : {report.IntentAccuracy:P2} (target {report.IntentTarget:P0})");
Console.WriteLine($"Tool Accuracy    : {report.ToolAccuracy:P2} (target {report.ToolTarget:P0})");
Console.WriteLine($"Intent Target OK : {report.PassedIntentTarget}");
Console.WriteLine($"Tool Target OK   : {report.PassedToolTarget}");
Console.WriteLine($"JSON Report      : {reportPath}");
Console.WriteLine($"CSV Report       : {csvPath}");

static T LoadJson<T>(string path)
{
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Missing required file: {path}");
    }

    return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonDefaults.CaseInsensitive)
        ?? throw new InvalidOperationException($"Unable to parse JSON file: {path}");
}

static string ResolveProjectRoot(string appBaseDirectory, string markerFile)
{
    var dir = new DirectoryInfo(appBaseDirectory);
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, markerFile);
        if (File.Exists(candidate))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    throw new InvalidOperationException($"Unable to find {markerFile} from {appBaseDirectory}");
}

static string BuildCsv(List<CaseResult> rows)
{
    var sb = new StringBuilder();
    sb.AppendLine("id,expectedIntent,predictedIntent,intentMatch,expectedTool,predictedTool,toolMatch");

    foreach (var row in rows)
    {
        sb.AppendLine(string.Join(",",
            Csv(row.Id),
            Csv(row.ExpectedIntent),
            Csv(row.PredictedIntent),
            row.IntentMatch,
            Csv(row.ExpectedTool),
            Csv(row.PredictedTool),
            row.ToolMatch));
    }

    return sb.ToString();
}

static string Csv(string? value)
{
    value ??= string.Empty;
    return $"\"{value.Replace("\"", "\"\"")}\"";
}

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static readonly JsonSerializerOptions WriteIndented = new()
    {
        WriteIndented = true
    };
}

internal sealed class ModelDecisionEvaluator
{
    private readonly HttpClient _httpClient;
    private readonly FoundryOptions _foundry;
    private readonly string _systemInstruction;
    private readonly EvalConfig _evalConfig;

    public ModelDecisionEvaluator(HttpClient httpClient, FoundryOptions foundry, string systemInstruction, EvalConfig evalConfig)
    {
        _httpClient = httpClient;
        _foundry = foundry;
        _systemInstruction = systemInstruction;
        _evalConfig = evalConfig;
    }

    public async Task<AgentDecision> PredictAsync(string query)
    {
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

Output schema:
{
  "intent": "...",
  "reply": "...",
  "requiresTool": true,
  "toolName": "...",
  "toolInput": {}
}
""";

        var messages = new object[]
        {
            new { role = "system", content = _systemInstruction },
            new { role = "system", content = decisionPrompt },
            new { role = "user", content = query }
        };

        var payload = new
        {
            model = _foundry.Deployment,
            messages,
            temperature = _evalConfig.ModelTemperature,
            max_tokens = _evalConfig.MaxTokens
        };

        var endpoint = BuildEndpoint(_foundry.Endpoint, _foundry.ApiVersion);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        request.Headers.Add("api-key", _foundry.ApiKey);

        using var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Foundry request failed: {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";

        var json = ExtractJson(content);
        var decision = JsonSerializer.Deserialize<AgentDecision>(json, JsonDefaults.CaseInsensitive)
            ?? new AgentDecision();

        decision.ToolName = NormalizeTool(decision.ToolName);
        decision.Intent = NormalizeIntent(decision.Intent);
        return decision;
    }

    private static string BuildEndpoint(string endpoint, string apiVersion)
    {
        endpoint = endpoint.TrimEnd('/');
        if (endpoint.Contains("?api-version=", StringComparison.OrdinalIgnoreCase))
        {
            return endpoint;
        }

        return $"{endpoint}?api-version={apiVersion}";
    }

    private static string ExtractJson(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            return "{}";
        }

        return raw[start..(end + 1)];
    }

    private static string NormalizeTool(string? tool)
    {
        if (string.IsNullOrWhiteSpace(tool))
        {
            return "None";
        }

        return tool.Trim() switch
        {
            "CreateServiceTicket" => "CreateServiceTicket",
            "GetContractDetails" => "GetContractDetails",
            "RenewContract" => "RenewContract",
            _ => "None"
        };
    }

    private static string NormalizeIntent(string? intent)
    {
        if (string.IsNullOrWhiteSpace(intent))
        {
            return "general_support";
        }

        return intent.Trim() switch
        {
            "mobile_repair" => "mobile_repair",
            "printer_sales" => "printer_sales",
            "contract_renewal" => "contract_renewal",
            _ => "general_support"
        };
    }
}

internal sealed class EvalConfig
{
    public string DatasetPath { get; set; } = "test-cases.json";
    public string OutputDirectory { get; set; } = "results";
    public double ModelTemperature { get; set; } = 0.1;
    public int MaxTokens { get; set; } = 300;
    public EvalMetrics Metrics { get; set; } = new();
}

internal sealed class EvalMetrics
{
    public double IntentAccuracyTarget { get; set; } = 0.85;
    public double ToolAccuracyTarget { get; set; } = 0.90;
}

internal sealed class EvalCase
{
    public string Id { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string ExpectedIntent { get; set; } = "general_support";
    public string ExpectedTool { get; set; } = "None";
}

internal sealed class AgentDecision
{
    public string Intent { get; set; } = "general_support";
    public string Reply { get; set; } = string.Empty;
    public bool RequiresTool { get; set; }
    public string ToolName { get; set; } = "None";
}

internal sealed class CaseResult
{
    public string Id { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string ExpectedIntent { get; set; } = string.Empty;
    public string PredictedIntent { get; set; } = string.Empty;
    public bool IntentMatch { get; set; }
    public string ExpectedTool { get; set; } = string.Empty;
    public string PredictedTool { get; set; } = string.Empty;
    public bool ToolMatch { get; set; }
    public string RawReply { get; set; } = string.Empty;
}

internal sealed class EvalReport
{
    public DateTime TimestampUtc { get; set; }
    public int TotalCases { get; set; }
    public double IntentAccuracy { get; set; }
    public double ToolAccuracy { get; set; }
    public double IntentTarget { get; set; }
    public double ToolTarget { get; set; }
    public bool PassedIntentTarget { get; set; }
    public bool PassedToolTarget { get; set; }
    public List<CaseResult> Cases { get; set; } = new();
}

internal sealed class AppOptions
{
    public FoundryOptions Foundry { get; set; } = new();
    public AgentOptions Agent { get; set; } = new();
}

internal sealed class FoundryOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string Deployment { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2024-05-01-preview";
    public string ApiKey { get; set; } = string.Empty;
}

internal sealed class AgentOptions
{
    public string SystemInstruction { get; set; } = string.Empty;
}
