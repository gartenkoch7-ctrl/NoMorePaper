using System.Text.Json.Nodes;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// JSON schema passed as <c>format</c> to Ollama (from the spike, <c>spikes/llm/Prompting/RecognitionSchema.cs</c>).
/// Ollama turns it into a grammar, so the answer is always valid JSON with exactly these fields, in this order.
/// </summary>
public static class OllamaSchema
{
    /// <summary>Most tags in an answer; with three, the third was mostly noise.</summary>
    public const int MaxTags = 2;

    /// <summary>A new object for each request, since JSON nodes belong to one parent.</summary>
    public static JsonObject Create()
    {
        // The grammar fixes the order, and the order matters (docs/kb/erkennung.md, "Schema"):
        // - correspondent and documentType first: asked before anything else, the model copies the
        //   spelling of the lists; after a summary it repeats the letterhead it has just written.
        // - summary before date and amount: writing it makes the model read the document before it
        //   commits to single values (asked first, it answered "amount": null and then named the
        //   amount in the summary).
        var properties = new JsonObject
        {
            ["correspondent"] = Nullable("string"),
            ["documentType"] = Nullable("string"),
            ["summary"] = new JsonObject { ["type"] = "string" },
            ["title"] = new JsonObject { ["type"] = "string" },
            ["documentDate"] = NullablePattern("^[0-9]{4}-[0-9]{2}-[0-9]{2}$"),
            ["amount"] = Nullable("number"),
            ["currency"] = NullablePattern("^[A-Z]{3}$"),
            ["tags"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["minItems"] = 1,
                // The models fill an array up to its limit.
                ["maxItems"] = MaxTags,
            },
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

    private static JsonObject Nullable(string type) =>
        new() { ["type"] = new JsonArray(type, "null") };

    private static JsonObject NullablePattern(string pattern) =>
        new() { ["type"] = new JsonArray("string", "null"), ["pattern"] = pattern };
}
