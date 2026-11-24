using System.Text.Json.Nodes;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// JSON schema of the stack separation, passed as <c>format</c> to Ollama, which turns it into a grammar: the
/// answer always has exactly these fields, in this order.
/// </summary>
public static class SeparationSchema
{
    /// <summary>A new object for each request, since JSON nodes belong to one parent.</summary>
    public static JsonObject Create()
    {
        // The senders first: the model names each page's sender before it decides where documents start,
        // so a change of sender is in front of it when it writes the boundaries.
        var properties = new JsonObject
        {
            ["senders"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } },
            ["boundaries"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "integer" } },
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
