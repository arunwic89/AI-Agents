namespace Function.Mcp;

using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

public class McpPrompts
{
    private readonly ILogger<McpPrompts> _logger;

    public McpPrompts(ILogger<McpPrompts> logger)
    {
        _logger = logger;
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
