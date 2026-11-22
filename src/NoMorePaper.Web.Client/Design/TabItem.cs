namespace NoMorePaper.Web.Client.Design;

/// <summary>One tab of a <see cref="TabList"/>; the id must be unique within the page.</summary>
/// <param name="Id">Unique within the page; the panel needs <c>{Id}-panel</c>.</param>
/// <param name="Label">The name of the tab.</param>
/// <param name="Icon">Optional MudBlazor icon in front of the name; decoration only (hidden from screen readers).</param>
/// <param name="Count">
/// Optional short statement next to the name, for example "1 problem". It is part of the accessible name
/// ("Label Count", taken from the visible text), so it must make sense when read aloud.
/// </param>
public sealed record TabItem(string Id, string Label, string? Icon = null, string? Count = null);
