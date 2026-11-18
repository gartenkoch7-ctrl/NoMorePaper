using System.Text.Json;
using System.Text.RegularExpressions;
using ICloudSpike.Http;

namespace ICloudSpike.Auth;

/// <summary>
/// Finds the trusted phone numbers in Apple's 2FA page. Apple answers either with JSON or with an
/// HTML page that carries the data in a script block of class "boot_args".
/// </summary>
internal static partial class TrustedPhoneParser
{
    public static IReadOnlyList<TrustedPhone> Parse(string body)
    {
        var phones = new List<TrustedPhone>();
        foreach (var candidate in JsonCandidates(body))
        {
            try
            {
                using var document = JsonDocument.Parse(candidate);
                Collect(document.RootElement, phones);
            }
            catch (JsonException)
            {
                // Not JSON after all; the caller falls back to the code from a trusted device.
            }
        }

        return phones;
    }

    private static IEnumerable<string> JsonCandidates(string body)
    {
        if (body.AsSpan().TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            yield return body;
            yield break;
        }

        foreach (Match match in BootArgsScript().Matches(body))
        {
            yield return match.Groups["json"].Value;
        }
    }

    /// <summary>
    /// Searches the whole document for "trustedPhoneNumbers" instead of following a fixed path:
    /// Apple moved the list in 2026 (under "bridgeInitiateData") and may move it again.
    /// </summary>
    private static void Collect(JsonElement element, List<TrustedPhone> phones)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("trustedPhoneNumbers"))
                    {
                        AddPhones(property.Value, phones);
                    }
                    else
                    {
                        Collect(property.Value, phones);
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, phones);
                }

                break;
        }
    }

    private static void AddPhones(JsonElement list, List<TrustedPhone> phones)
    {
        foreach (var entry in ((JsonElement?)list).Items())
        {
            JsonElement? phone = entry;
            var id = phone.Child("id").Number();
            var masked = phone.Child("obfuscatedNumber").Text() ?? phone.Child("numberWithDialCode").Text();
            if (id is not null && masked is not null && phones.TrueForAll(known => known.Id != id))
            {
                phones.Add(new TrustedPhone((int)id.Value, masked));
            }
        }
    }

    [GeneratedRegex(
        """<script[^>]*class\s*=\s*["'][^"']*boot_args[^"']*["'][^>]*>(?<json>.*?)</script>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex BootArgsScript();
}
