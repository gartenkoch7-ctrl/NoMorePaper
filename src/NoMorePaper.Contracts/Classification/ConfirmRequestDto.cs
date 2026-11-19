using System.Text.Json.Serialization;

namespace NoMorePaper.Contracts.Classification;

/// <summary>Body of <c>POST /api/documents/{id}/confirm</c>.</summary>
/// <param name="Recognition">
/// The <see cref="DocumentFieldsDto.Recognition"/> the client showed the fields with; required. When the
/// document has another one by now (recognized, or confirmed for the first time, meanwhile), the server
/// answers 409 instead of confirming values the person has not seen.
/// </param>
public sealed record ConfirmRequestDto([property: JsonRequired] DocumentRecognitionState Recognition);
