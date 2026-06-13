namespace Function.Mcp;

using System;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

public class McpResources
{
    private readonly ILogger<McpResources> _logger;
    private static readonly ConcurrentDictionary<string, string> Notes = new();

    private const string NotesResourceMetadata = """
        {
            "source": "in-memory",
            "owner": "MCP demo",
            "kind": "knowledge-resource"
        }
        """;

    public McpResources(ILogger<McpResources> logger)
    {
        _logger = logger;
    }

    [Function(nameof(ReadNotesResource))]
    public string ReadNotesResource(
        [McpResourceTrigger(
            "memory://notes",
            "notes",
            Description = "Read all notes saved by the save_note MCP tool.",
            MimeType = "application/json")]
        [McpMetadata(NotesResourceMetadata)] ResourceInvocationContext context)
    {
        _logger.LogInformation("Reading MCP notes resource");

        var payload = new
        {
            status = "success",
            count = Notes.Count,
            notes = Notes
        };

        return JsonSerializer.Serialize(payload);
    }
}
