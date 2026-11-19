using System.Diagnostics.CodeAnalysis;
using NoMorePaper.Domain.Classification;

namespace NoMorePaper.Application.Classification;

/// <summary>Checks a name from a request against <see cref="ClassificationName.Clean"/> without exceptions. Pure logic.</summary>
internal static class EntryNames
{
    /// <returns>Whether the name is valid; <paramref name="cleaned"/> is the trimmed name then.</returns>
    public static bool TryClean(string? name, [NotNullWhen(true)] out string? cleaned)
    {
        cleaned = null;
        if (name is null)
        {
            return false;
        }

        try
        {
            cleaned = ClassificationName.Clean(name, nameof(name));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
