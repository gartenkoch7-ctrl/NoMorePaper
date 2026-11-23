using System.Text.Json.Serialization;

namespace NoMorePaper.Contracts.Editing;

/// <summary>Which versions an edited document keeps; serialized as its name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<VersionPolicyDto>))]
public enum VersionPolicyDto
{
    /// <summary>Original and newest edited version.</summary>
    KeepBoth,

    /// <summary>Only the edited version; the original goes to the trash.</summary>
    EditedOnly,

    /// <summary>Only the original; the edited version goes to the trash.</summary>
    OriginalOnly,
}
