using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using NoMorePaper.Application.Editing;
using NoMorePaper.Contracts.Editing;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Server.Editing;

/// <summary>
/// The form of <c>POST /api/documents/{id}/edit</c> and <c>…/split</c>: a text field <c>plan</c> with the
/// <see cref="PagePlanDto"/> or <see cref="SplitPlanDto"/> as JSON (camel case, enums by name) and one file part
/// <c>page-&lt;n&gt;</c> per replaced page n. Up to <see cref="EditPageImage.MaxImages"/> images of up to
/// <see cref="EditPageImage.MaxBytes"/>; the use case checks that they match the plan and are JPEGs. A split may
/// also come as a plain JSON body without images (an accepted split suggestion).
/// </summary>
internal static class PageEditForm
{
    public const string PlanField = "plan";
    public const string PagePrefix = "page-";

    /// <summary>All images at their largest plus room for the plan and the multipart framing.</summary>
    public const long MaxRequestBytes = (EditPageImage.MaxImages * EditPageImage.MaxBytes) + (1024 * 1024);

    // A plan alone, as JSON body: some KB even for hundreds of pages.
    private const long MaxJsonBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    /// <returns>The page plan and images, or a fixed reason word and the HTTP status of the refusal.</returns>
    public static Task<(PagePlan? Plan, IReadOnlyList<EditPageImage> Images, string? Reason, int Status)> ReadPagesAsync(
        HttpRequest request, CancellationToken cancellationToken) =>
        ReadAsync(request, json => EditingMapping.ToDomain(JsonSerializer.Deserialize<PagePlanDto>(json, s_json)), cancellationToken);

    /// <returns>The split and images, or a fixed reason word and the HTTP status of the refusal.</returns>
    public static async Task<(SplitPlan? Plan, IReadOnlyList<EditPageImage> Images, string? Reason, int Status)> ReadSplitAsync(
        HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.HasFormContentType)
        {
            return await ReadAsync(request, json => EditingMapping.ToDomain(JsonSerializer.Deserialize<SplitPlanDto>(json, s_json)), cancellationToken)
                .ConfigureAwait(false);
        }

        if (request.ContentLength > MaxJsonBytes)
        {
            return (null, [], "too-large", StatusCodes.Status413PayloadTooLarge);
        }

        try
        {
            var body = await request.ReadFromJsonAsync<SplitPlanDto>(s_json, cancellationToken).ConfigureAwait(false);
            return EditingMapping.ToDomain(body) is { } plan
                ? (plan, [], null, StatusCodes.Status200OK)
                : (null, [], "plan", StatusCodes.Status400BadRequest);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or BadHttpRequestException)
        {
            return (null, [], "plan", StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<(TPlan? Plan, IReadOnlyList<EditPageImage> Images, string? Reason, int Status)> ReadAsync<TPlan>(
        HttpRequest request, Func<string, TPlan?> parse, CancellationToken cancellationToken)
        where TPlan : class
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ContentLength > MaxRequestBytes)
        {
            return (null, [], "too-large", StatusCodes.Status413PayloadTooLarge);
        }

        // The defaults (Kestrel 30 MB, the app's form limit for uploads) would cut off valid requests.
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = MaxRequestBytes;
        }

        if (!request.HasFormContentType)
        {
            return (null, [], "not-multipart", StatusCodes.Status400BadRequest);
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = MaxRequestBytes }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return (null, [], "too-large", StatusCodes.Status413PayloadTooLarge);
        }
        catch (Exception exception) when (exception is InvalidDataException or BadHttpRequestException)
        {
            return (null, [], "malformed", StatusCodes.Status400BadRequest);
        }

        var plan = form[PlanField] is { Count: 1 } values ? Parse(values[0], parse) : null;
        if (plan is null)
        {
            return (null, [], "plan", StatusCodes.Status400BadRequest);
        }

        List<EditPageImage> images = [];
        foreach (var file in form.Files)
        {
            if (!file.Name.StartsWith(PagePrefix, StringComparison.Ordinal)
                || !int.TryParse(file.Name.AsSpan(PagePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var page))
            {
                return (null, [], "unknown-part", StatusCodes.Status400BadRequest);
            }

            if (file.Length > EditPageImage.MaxBytes)
            {
                return (null, [], "too-large", StatusCodes.Status413PayloadTooLarge);
            }

            images.Add(new EditPageImage(page, file.Length, file.OpenReadStream));
        }

        return (plan, images, null, StatusCodes.Status200OK);
    }

    private static TPlan? Parse<TPlan>(string? json, Func<string, TPlan?> parse)
        where TPlan : class
    {
        try
        {
            return json is null ? null : parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
