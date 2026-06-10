using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

var serverProject = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SimpleMcpServer", "SimpleMcpServer.csproj"));

var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"run --project \"{serverProject}\"",
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    }
};

process.ErrorDataReceived += (_, e) =>
{
    if (!string.IsNullOrWhiteSpace(e.Data))
    {
        Console.Error.WriteLine($"[server] {e.Data}");
    }
};

process.Start();
process.BeginErrorReadLine();

var nextId = 1;

Console.WriteLine("============================================================");
Console.WriteLine("Testing C# MCP Server");
Console.WriteLine("============================================================");

try
{
    var init = await SendRequestAsync(process, nextId++, "initialize", new
    {
        protocolVersion = "2024-11-05",
        capabilities = new { },
        clientInfo = new { name = "simple-csharp-client", version = "1.0.0" }
    });

    Console.WriteLine("\nConnected and initialized.");
    PrintJson("Initialize response", init);

    var toolsList = await SendRequestAsync(process, nextId++, "tools/list", new { });
    PrintJson("Tools list", toolsList);

    var getUser = await SendRequestAsync(process, nextId++, "tools/call", new
    {
        name = "get_user",
        arguments = new { user_id = 1 }
    });
    PrintJson("Get user", getUser);

    var listUsers = await SendRequestAsync(process, nextId++, "tools/call", new
    {
        name = "list_all_users",
        arguments = new { }
    });
    PrintJson("List all users", listUsers);

    var addUser = await SendRequestAsync(process, nextId++, "tools/call", new
    {
        name = "add_user",
        arguments = new { name = "Charlie", email = "charlie@example.com" }
    });
    PrintJson("Add user", addUser);

    var getAddedUser = await SendRequestAsync(process, nextId++, "tools/call", new
    {
        name = "get_user",
        arguments = new { user_id = 3 }
    });
    PrintJson("Get newly added user", getAddedUser);

    var missingUser = await SendRequestAsync(process, nextId++, "tools/call", new
    {
        name = "get_user",
        arguments = new { user_id = 999 }
    });
    PrintJson("Get missing user (error path)", missingUser);

    Console.WriteLine("\nAll C# MCP tests completed.");
}
finally
{
    if (!process.HasExited)
    {
        process.Kill(entireProcessTree: true);
    }

    process.Dispose();
}

static async Task<JsonElement> SendRequestAsync(Process process, int id, string method, object @params)
{
    var request = new
    {
        jsonrpc = "2.0",
        id,
        method,
        @params
    };

    var requestJson = JsonSerializer.Serialize(request);
    await process.StandardInput.WriteLineAsync(requestJson);
    await process.StandardInput.FlushAsync();

    while (true)
    {
        var line = await process.StandardOutput.ReadLineAsync();
        if (line is null)
        {
            throw new InvalidOperationException("Server closed output unexpectedly.");
        }

        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        if (!root.TryGetProperty("id", out var idElement) || idElement.GetInt32() != id)
        {
            continue;
        }

        if (root.TryGetProperty("error", out var errorElement))
        {
            throw new InvalidOperationException($"RPC error for method {method}: {errorElement}");
        }

        if (!root.TryGetProperty("result", out var result))
        {
            throw new InvalidOperationException($"No result for method {method}");
        }

        return JsonDocument.Parse(result.GetRawText()).RootElement.Clone();
    }
}

static void PrintJson(string title, JsonElement value)
{
    Console.WriteLine($"\n{title}:");
    Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}
