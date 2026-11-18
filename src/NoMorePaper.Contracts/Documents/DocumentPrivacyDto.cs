using System.Text.Json.Serialization;

namespace NoMorePaper.Contracts.Documents;

/// <summary>
/// Body of <c>PATCH /api/documents/{id}</c> (owner only). Required: a request without it is refused
/// instead of silently sharing a private document.
/// </summary>
public sealed record DocumentPrivacyDto([property: JsonRequired] bool IsPrivate);
