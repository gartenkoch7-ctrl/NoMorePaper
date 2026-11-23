namespace NoMorePaper.Web.Client.Design;

/// <summary>One step of a <see cref="Breadcrumb"/>.</summary>
/// <param name="Label">The name of the page.</param>
/// <param name="Href">Link target relative to the base address; <c>null</c> marks the current page.</param>
public sealed record BreadcrumbItem(string Label, string? Href);
