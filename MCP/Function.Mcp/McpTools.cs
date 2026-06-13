namespace Function.Mcp;

using System;
using System.Collections.Concurrent;
using Microsoft.Azure.Functions.Worker; 
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

public class McpTools
{
    private readonly ILogger<McpTools> _logger;
    private static readonly ConcurrentDictionary<string, string> Notes = new();

    public McpTools(ILogger<McpTools> logger)
    {
        _logger = logger;
    }


    [Function(nameof(SaveNoteTool))]
    public object SaveNoteTool(
        [McpToolTrigger("save_note", "Save a note that can be read using the notes MCP resource.")] ToolInvocationContext context,
        [McpToolProperty("id", "Unique note identifier", IsRequired = true)] string id,
        [McpToolProperty("content", "Note content", IsRequired = true)] string content)
    {
        var noteId = id?.Trim();
        var noteContent = content?.Trim();

        if (string.IsNullOrWhiteSpace(noteId))
        {
            return new { status = "error", message = "id is required" };
        }

        if (string.IsNullOrWhiteSpace(noteContent))
        {
            return new { status = "error", message = "content is required" };
        }

        Notes[noteId] = noteContent;
        _logger.LogInformation("Saved MCP note with id {NoteId}", noteId);

        return new
        {
            status = "success",
            id = noteId,
            resourceUri = "memory://notes"
        };
    }
}
