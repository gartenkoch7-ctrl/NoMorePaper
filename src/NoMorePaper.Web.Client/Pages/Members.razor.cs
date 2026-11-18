using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Web.Client.Accessibility;
using NoMorePaper.Web.Client.Accounts;

namespace NoMorePaper.Web.Client.Pages;

/// <summary>
/// <c>/mitglieder</c> (administrator only): invite members, create reset links, remove members.
/// Every result is announced; after each action focus lands where the person continues.
/// </summary>
public sealed partial class Members
{
    private LiveRegion? _status;
    private LiveRegion? _error;
    private ElementReference _inviteButton;
    private ElementReference _cancelButton;
    private CopyField? _inviteLink;
    private CopyField? _resetField;
    private MemberDto[] _members = [];
    private InvitationLinkDto? _invitation;
    private InvitationLinkDto? _resetLink;
    private MemberDto? _resetFor;
    private MemberDto? _removing;
    private string? _loadError;
    private bool _loading = true;
    private Func<ValueTask>? _focusAfterRender;

    protected override async Task OnInitializedAsync()
    {
        if (Auth.IsAdmin)
        {
            await LoadAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Focus can only move once the element it goes to has been rendered.
        if (_focusAfterRender is { } focus)
        {
            _focusAfterRender = null;
            await focus();
        }
    }

    private async Task LoadAsync()
    {
        var result = await Api.ListMembersAsync();
        _loadError = result.Error;
        _members = result.Value ?? [];
        _loading = false;
    }

    private string Absolute(string link) => Uri.TryCreate(link, UriKind.Absolute, out var uri)
        ? uri.ToString()
        : Navigation.ToAbsoluteUri(link).ToString();

    private static string Expires(InvitationLinkDto link) =>
        link.ExpiresAt.ToLocalTime().ToString("f", CultureInfo.CurrentCulture);

    private async Task InviteAsync()
    {
        _error?.Clear();
        var result = await Api.InviteAsync();
        if (!result.IsSuccess)
        {
            await FailAsync(result.Error!);
            return;
        }

        Logger.InvitationCreated();
        _invitation = result.Value;
        _focusAfterRender = () => _inviteLink?.FocusAsync() ?? ValueTask.CompletedTask;
        await AnnounceAsync(L["MembersInviteCreated"]);
    }

    private async Task ResetLinkAsync(MemberDto member)
    {
        _error?.Clear();
        var result = await Api.CreateResetLinkAsync(member.Id);
        if (!result.IsSuccess)
        {
            await FailAsync(result.Error!);
            return;
        }

        Logger.ResetLinkCreated();
        (_resetFor, _resetLink) = (member, result.Value);
        _focusAfterRender = () => _resetField?.FocusAsync() ?? ValueTask.CompletedTask;
        await AnnounceAsync(L["MembersResetCreated", member.DisplayName]);
    }

    private void AskRemove(MemberDto member)
    {
        _removing = member;
        _focusAfterRender = () => _cancelButton.FocusAsync();
    }

    private void CancelRemove()
    {
        _removing = null;
        _focusAfterRender = () => _inviteButton.FocusAsync();
    }

    private void OnConfirmKey(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
        {
            CancelRemove();
        }
    }

    private async Task RemoveAsync()
    {
        if (_removing is not { } member)
        {
            return;
        }

        var result = await Api.RemoveMemberAsync(member.Id);
        _removing = null;
        if (!result.IsSuccess)
        {
            await FailAsync(result.Error!);
            _focusAfterRender = () => _inviteButton.FocusAsync();
            return;
        }

        Logger.MemberRemoved();
        if (_resetFor?.Id == member.Id)
        {
            (_resetFor, _resetLink) = (null, null);
        }

        await LoadAsync();
        _focusAfterRender = () => _inviteButton.FocusAsync();
        await AnnounceAsync(L["MembersRemoved", member.DisplayName]);
    }

    private async Task AnnounceAsync(string message)
    {
        if (_status is not null)
        {
            await _status.Announce(message);
        }
    }

    private async Task FailAsync(string message)
    {
        if (_error is not null)
        {
            await _error.Announce(L["ErrorPrefix", message]);
        }
    }
}
