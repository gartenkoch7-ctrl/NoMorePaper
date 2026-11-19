using System.Text.Json.Nodes;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// JSON schema of the mail check, passed as <c>format</c> to Ollama, which turns it into a grammar: the
/// answer always has exactly these fields, in this order.
/// </summary>
public static class MailSchema
{
    /// <summary>A new object for each request, since JSON nodes belong to one parent.</summary>
    public static JsonObject Create()
    {
        // The verdicts first: they are what the run decides on; the note is written knowing them, so
        // a spam mail gets no note.
        var properties = new JsonObject
        {
            ["isSpam"] = new JsonObject { ["type"] = "boolean" },
            ["isDocument"] = new JsonObject { ["type"] = "boolean" },
            ["note"] = new JsonObject { ["type"] = "string" },
        };

        var required = new JsonArray();
        foreach (var property in properties)
        {
            required.Add(property.Key);
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false,
        };
    }
}
