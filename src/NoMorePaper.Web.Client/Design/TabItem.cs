namespace NoMorePaper.Web.Client.Design;

/// <summary>One tab of a <see cref="TabList"/>; the id must be unique within the page.</summary>
public sealed record TabItem(string Id, string Label);
