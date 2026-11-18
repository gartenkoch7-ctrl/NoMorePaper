using System.Net.Http.Json;
using System.Text.Json;

namespace NoMorePaper.Web.Client.Accounts;

/// <summary>
/// The first problem of a refused account input. The server answers 400 with
/// <c>errors: { field: [word] }</c>, where the word is one of <c>invalid</c>, <c>taken</c>,
/// <c>too-short</c>, <c>wrong</c> or <c>self</c>; the client turns it into its own text.
/// </summary>
/// <param name="Field">Request field in camelCase, e.g. <c>userName</c> or <c>newPassword</c>.</param>
public sealed record RefusedInput(string Field, string Word)
{
    /// <summary>Resource key of the message for this problem; unknown combinations get a general text.</summary>
    public string MessageKey => (Field, Word) switch
    {
        ("userName", "invalid") => "ErrorUserNameInvalid",
        ("userName", "taken") => "ErrorUserNameTaken",
        ("displayName", "invalid") => "ErrorDisplayNameInvalid",
        (_, "too-short") => "ErrorPasswordShort",
        ("currentPassword", "wrong") => "ErrorCurrentPasswordWrong",
        (_, "self") => "ErrorRemoveSelf",
        _ => "ErrorRejected",
    };

    /// <returns>The first problem, or <c>null</c> when the body is no validation problem.</returns>
    public static async Task<RefusedInput?> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var body = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
            return body is not null ? First(body.RootElement) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static RefusedInput? First(JsonElement problem)
    {
        if (problem.ValueKind != JsonValueKind.Object
            || !problem.TryGetProperty("errors", out var errors)
            || errors.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var field in errors.EnumerateObject())
        {
            if (field.Value.ValueKind == JsonValueKind.Array
                && field.Value.EnumerateArray().FirstOrDefault() is { ValueKind: JsonValueKind.String } word)
            {
                return new RefusedInput(field.Name, word.GetString()!);
            }
        }

        return null;
    }
}
