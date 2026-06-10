using System.Text.Json;
using System.Text.Json.Nodes;

var users = new Dictionary<int, User>
{
    [1] = new User(1, "Alice", "alice@example.com"),
    [2] = new User(2, "Bob", "bob@example.com")
};

var serializerOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

while (true)
{
    var line = await Console.In.ReadLineAsync();
    if (line is null)
    {
        break;
    }

    if (string.IsNullOrWhiteSpace(line))
    {
        continue;
    }

    JsonDocument requestDoc;
    try
    {
        requestDoc = JsonDocument.Parse(line);
    }
    catch
    {
        continue;
    }

    using (requestDoc)
    {
        var request = requestDoc.RootElement;
        if (!request.TryGetProperty("method", out var methodElement))
        {
            continue;
        }

        var method = methodElement.GetString();
        if (string.IsNullOrEmpty(method))
        {
            continue;
        }

        var hasId = request.TryGetProperty("id", out var idElement);

        // Notifications do not require response.
        if (!hasId && (method == "notifications/initialized" || method == "$/cancelRequest"))
        {
            continue;
        }

        object response;
        try
        {
            response = method switch
            {
                "initialize" => Success(idElement, BuildInitializeResult()),
                "tools/list" => Success(idElement, BuildToolsListResult()),
                "tools/call" => HandleToolCall(idElement, request, users),
                _ => Error(idElement, -32601, $"Method not found: {method}")
            };
        }
        catch (Exception ex)
        {
            response = Error(idElement, -32000, ex.Message);
        }

        var json = JsonSerializer.Serialize(response, serializerOptions);
        await Console.Out.WriteLineAsync(json);
        await Console.Out.FlushAsync();
    }
}

static object BuildInitializeResult()
{
    return new
    {
        protocolVersion = "2024-11-05",
        capabilities = new
        {
            tools = new
            {
                listChanged = false
            }
        },
        serverInfo = new
        {
            name = "simple-csharp-mcp-server",
            version = "1.0.0"
        }
    };
}

static object BuildToolsListResult()
{
    object[] tools =
    {
        new
        {
            name = "get_user",
            description = "Get a user by their ID",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    user_id = new
                    {
                        type = "integer",
                        description = "User ID"
                    }
                },
                required = new[] { "user_id" }
            }
        },
        new
        {
            name = "list_all_users",
            description = "List all users in the database",
            inputSchema = new
            {
                type = "object",
                properties = new { }
            }
        },
        new
        {
            name = "add_user",
            description = "Add a new user",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string" },
                    email = new { type = "string" }
                },
                required = new[] { "name", "email" }
            }
        }
    };

    return new
    {
        tools
    };
}

static object HandleToolCall(JsonElement id, JsonElement request, Dictionary<int, User> users)
{
    if (!request.TryGetProperty("params", out var paramsElement))
    {
        return Error(id, -32602, "Missing params");
    }

    if (!paramsElement.TryGetProperty("name", out var toolNameElement))
    {
        return Error(id, -32602, "Missing params.name");
    }

    var toolName = toolNameElement.GetString();
    var args = paramsElement.TryGetProperty("arguments", out var argsElement)
        ? argsElement
        : default;

    ToolResult result;

    switch (toolName)
    {
        case "get_user":
        {
            if (!TryGetInt(args, "user_id", out var userId))
            {
                result = ToolResult.Error("Invalid or missing user_id");
                break;
            }

            if (!users.TryGetValue(userId, out var user))
            {
                result = ToolResult.Error($"User {userId} not found");
                break;
            }

            result = ToolResult.Success(new { status = "success", user });
            break;
        }
        case "list_all_users":
        {
            result = ToolResult.Success(new { status = "success", users = users.Values.ToArray() });
            break;
        }
        case "add_user":
        {
            if (!TryGetString(args, "name", out var name) || string.IsNullOrWhiteSpace(name))
            {
                result = ToolResult.Error("Invalid or missing name");
                break;
            }

            if (!TryGetString(args, "email", out var email) || string.IsNullOrWhiteSpace(email))
            {
                result = ToolResult.Error("Invalid or missing email");
                break;
            }

            var newId = users.Count == 0 ? 1 : users.Keys.Max() + 1;
            var newUser = new User(newId, name.Trim(), email.Trim());
            users[newId] = newUser;
            result = ToolResult.Success(new { status = "success", user = newUser });
            break;
        }
        default:
        {
            result = ToolResult.Error($"Unknown tool: {toolName}");
            break;
        }
    }

    return Success(id, new
    {
        content = new[]
        {
            new
            {
                type = "text",
                text = JsonSerializer.Serialize(result.Payload)
            }
        },
        isError = result.IsError
    });
}

static bool TryGetInt(JsonElement args, string key, out int value)
{
    value = default;
    if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(key, out var element))
    {
        return false;
    }

    return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
}

static bool TryGetString(JsonElement args, string key, out string value)
{
    value = string.Empty;
    if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(key, out var element))
    {
        return false;
    }

    if (element.ValueKind != JsonValueKind.String)
    {
        return false;
    }

    value = element.GetString() ?? string.Empty;
    return true;
}

static object Success(JsonElement id, object result)
{
    return new
    {
        jsonrpc = "2.0",
        id = JsonNode.Parse(id.GetRawText()),
        result
    };
}

static object Error(JsonElement id, int code, string message)
{
    return new
    {
        jsonrpc = "2.0",
        id = id.ValueKind == JsonValueKind.Undefined ? null : JsonNode.Parse(id.GetRawText()),
        error = new
        {
            code,
            message
        }
    };
}

internal readonly record struct User(int Id, string Name, string Email);

internal readonly record struct ToolResult(bool IsError, object Payload)
{
    public static ToolResult Success(object payload) => new(false, payload);
    public static ToolResult Error(string message) => new(true, new { status = "error", message });
}
