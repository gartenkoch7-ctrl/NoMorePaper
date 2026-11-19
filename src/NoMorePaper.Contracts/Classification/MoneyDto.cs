namespace NoMorePaper.Contracts.Classification;

/// <summary>An amount with its currency.</summary>
/// <param name="Value">The amount; negative for credit notes, at most four decimal places.</param>
/// <param name="Currency">ISO 4217 code, e.g. <c>EUR</c>.</param>
public sealed record MoneyDto(decimal Value, string Currency);
