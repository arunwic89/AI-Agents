using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// In-memory demo store so each tool call can read/write the same state.
var usersStore = new UsersStore();

var builder = Host.CreateApplicationBuilder(args);
// Stdio MCP servers should keep stdout clean for JSON-RPC frames.
builder.Logging.ClearProviders();

// SDK-style MCP setup:
// 1) Register dependencies
// 2) Add MCP server
// 3) Choose transport (stdio)
// 4) Register tools from annotated class
builder.Services
	.AddSingleton(usersStore)
	.AddMcpServer()
	.WithStdioServerTransport()
	.WithTools<DemoTools>();

await builder.Build().RunAsync();

// Marks this class as a container for MCP tools.
[McpServerToolType]
public sealed class DemoTools(UsersStore store)
{
	// Exposed as MCP tool: get_user
	[McpServerTool(Name = "get_user"), Description("Get a user by their ID")]
	public object GetUser([Description("User ID")] int user_id)
	{
		if (!store.Users.TryGetValue(user_id, out var user))
		{
			return Error($"User {user_id} not found");
		}

		return new { status = "success", user };
	}

	// Exposed as MCP tool: list_all_users
	[McpServerTool(Name = "list_all_users"), Description("List all users in the database")]
	public object ListAllUsers()
	{
		return new { status = "success", users = store.Users.Values.ToArray() };
	}

	// Exposed as MCP tool: add_user
	[McpServerTool(Name = "add_user"), Description("Add a new user")]
	public object AddUser(
		[Description("User name")] string name,
		[Description("User email")] string email)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return Error("Invalid or missing name");
		}

		if (string.IsNullOrWhiteSpace(email))
		{
			return Error("Invalid or missing email");
		}

		var newId = store.Users.Count == 0 ? 1 : store.Users.Keys.Max() + 1;
		var user = new User(newId, name.Trim(), email.Trim());
		store.Users[newId] = user;

		return new { status = "success", user };
	}

	private static object Error(string message) => new { status = "error", message };
}

// Minimal state container for the demo.
public sealed class UsersStore
{
	public Dictionary<int, User> Users { get; } = new()
	{
		[1] = new User(1, "Alice", "alice@example.com"),
		[2] = new User(2, "Bob", "bob@example.com")
	};
}

public readonly record struct User(int Id, string Name, string Email);
