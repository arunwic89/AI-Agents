using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace Function.Mcp;

public class McpEndToEndSample
{
    private readonly ILogger<McpEndToEndSample> _logger;

    // Simple in-memory storage for demo purposes.
    private static readonly ConcurrentDictionary<string, string> Notes = new();

    private const string NotesResourceMetadata = """
        {
            "source": "in-memory",
            "owner": "MCP demo",
            "kind": "knowledge-resource"
        }
        """;

    public McpEndToEndSample(ILogger<McpEndToEndSample> logger)
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

    [Function(nameof(ComposeNotePlanPrompt))]
    public string ComposeNotePlanPrompt(
        [McpPromptTrigger("compose_note_plan", Description = "Builds a prompt that guides the agent to use save_note and memory://notes.")] PromptInvocationContext context,
        [McpPromptArgument("goal", "What the note should capture.", IsRequired = true)] string goal,
        [McpPromptArgument("tone", "Preferred writing tone.", IsRequired = false)] string? tone)
    {
        _logger.LogInformation("MCP Prompt function triggered.");

        var selectedTone = string.IsNullOrWhiteSpace(tone) ? "clear and concise" : tone.Trim();

        return $"""
You are preparing notes for a user.

Goal:
- {goal}

Instructions:
1. Draft the content in a {selectedTone} tone.
2. Save the note by calling tool `save_note` with:
   - id: a short slug for the topic
   - content: the drafted note
3. Read resource `memory://notes` to verify the note was stored.
4. Return the final confirmation to the user.
""";
    }
}