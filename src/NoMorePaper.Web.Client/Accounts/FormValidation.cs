using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using NoMorePaper.Web.Client.Accessibility;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Accounts;

/// <summary>
/// Checks a form and reports the first problem the way a screen reader user needs it: the error
/// text is announced (and stays visible) and focus moves to the field that has to change.
/// Only the first problem is reported, so the announcement is one short sentence.
/// </summary>
public sealed class FormValidation(IStringLocalizer<Strings> localizer, IJSRuntime js)
{
    /// <summary>Same minimum as the server (plan m2-konten); there are no other password rules.</summary>
    public const int MinPasswordLength = 10;

    /// <summary>Id of the field that has the first problem, or null.</summary>
    public string? InvalidId { get; private set; }

    public string? Message { get; private set; }

    public bool HasIssue => Message is not null;

    public bool IsInvalid(string id) => InvalidId == id;

    public void Reset() => (InvalidId, Message) = (null, null);

    public void Required(string id, string value, string label)
    {
        if (!HasIssue && string.IsNullOrWhiteSpace(value))
        {
            Set(id, localizer["ErrorRequired", label]);
        }
    }

    /// <summary>A new password: long enough, and typed twice the same.</summary>
    public void NewPassword(string passwordId, string password, string repeatId, string repeat, string label)
    {
        Required(passwordId, password, label);
        if (!HasIssue && password.Length < MinPasswordLength)
        {
            Set(passwordId, localizer["ErrorPasswordShort", MinPasswordLength]);
        }

        if (!HasIssue && password != repeat)
        {
            Set(repeatId, localizer["ErrorPasswordMismatch"]);
        }
    }

    public void Set(string id, string message) => (InvalidId, Message) = (id, message);

    /// <summary>Announces the problem and moves focus to its field. Returns false when there is none.</summary>
    public async Task<bool> ReportAsync(LiveRegion? region)
    {
        if (!HasIssue)
        {
            return false;
        }

        if (region is not null)
        {
            await region.Announce(localizer["ErrorPrefix", Message!]);
        }

        if (InvalidId is not null)
        {
            await js.InvokeVoidAsync("nmpForms.focusById", InvalidId);
        }

        return true;
    }
}
