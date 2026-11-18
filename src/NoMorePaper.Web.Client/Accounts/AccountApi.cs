using System.Text.Json;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Accounts;

/// <summary>
/// Typed client for the account routes (setup, sign-in, invitations, members, password reset).
/// Methods without a response body return <c>ApiResult&lt;bool&gt;</c> (true on success).
/// Tokens of links are part of the path only; they are never logged.
/// </summary>
public sealed class AccountApi
{
    private readonly AccountHttp _http;
    private readonly IStringLocalizer<Strings> _localizer;

    public AccountApi(HttpClient http, IStringLocalizer<Strings> localizer, ILogger<AccountApi> logger)
    {
        _http = new AccountHttp(http, localizer, logger);
        _localizer = localizer;
    }

    public Task<ApiResult<SetupStatusDto>> GetSetupAsync(CancellationToken ct = default)
        => _http.SendAsync<SetupStatusDto>(HttpMethod.Get, "api/setup", null, "setup-status", ct);

    public Task<ApiResult<EmergencyCodeDto>> SetUpAsync(SetupDto request, CancellationToken ct = default)
        => _http.SendAsync<EmergencyCodeDto>(HttpMethod.Post, "api/setup", request, "setup", ct);

    public Task<ApiResult<bool>> LoginAsync(SignInDto request, CancellationToken ct = default)
        => _http.SendAsync<bool>(HttpMethod.Post, "api/auth/login", request, "login", ct);

    public Task<ApiResult<bool>> LogoutAsync(bool everywhere, CancellationToken ct = default)
        => _http.SendAsync<bool>(
            HttpMethod.Post, everywhere ? "api/auth/logout-everywhere" : "api/auth/logout", null, "logout", ct);

    public async Task<ApiResult<SignedInAccount>> GetMeAsync(CancellationToken ct = default)
    {
        // Read as JSON first: the list of administrator resets is taken from it tolerantly.
        var answer = await _http.SendAsync<JsonElement>(HttpMethod.Get, "api/auth/me", null, "me", ct);
        if (!answer.IsSuccess)
        {
            return new ApiResult<SignedInAccount>(default, answer.Error, answer.Field);
        }

        return SignedInAccount.Read(answer.Value) is { } me
            ? new ApiResult<SignedInAccount>(me, null)
            : new ApiResult<SignedInAccount>(default, _localizer["ErrorUnreachable"]);
    }

    public Task<ApiResult<bool>> ChangePasswordAsync(ChangePasswordDto request, CancellationToken ct = default)
        => _http.SendAsync<bool>(HttpMethod.Post, "api/account/password", request, "change-password", ct);

    /// <summary>Confirms the notice that the administrator's reset link changed the password.</summary>
    public Task<ApiResult<bool>> AcknowledgeResetAsync(CancellationToken ct = default)
        => _http.SendAsync<bool>(HttpMethod.Post, "api/account/acknowledge-reset", null, "acknowledge-reset", ct);

    public Task<ApiResult<LinkDto>> InviteAsync(CancellationToken ct = default)
        => _http.SendAsync<LinkDto>(HttpMethod.Post, "api/invitations", null, "invite", ct);

    public Task<ApiResult<InvitationDto>> GetInvitationAsync(string token, CancellationToken ct = default)
        => _http.SendAsync<InvitationDto>(
            HttpMethod.Get, $"api/invitations/{Uri.EscapeDataString(token)}", null, "invitation", ct);

    public Task<ApiResult<bool>> AcceptInvitationAsync(
        string token, AcceptInvitationDto request, CancellationToken ct = default)
        => _http.SendAsync<bool>(
            HttpMethod.Post, $"api/invitations/{Uri.EscapeDataString(token)}/accept", request, "accept", ct);

    public Task<ApiResult<AccountDto[]>> ListMembersAsync(CancellationToken ct = default)
        => _http.SendAsync<AccountDto[]>(HttpMethod.Get, "api/members", null, "members", ct);

    public Task<ApiResult<bool>> RemoveMemberAsync(Guid id, CancellationToken ct = default)
        => _http.SendAsync<bool>(HttpMethod.Delete, $"api/members/{id}", null, "remove-member", ct);

    public Task<ApiResult<LinkDto>> CreateResetLinkAsync(Guid id, CancellationToken ct = default)
        => _http.SendAsync<LinkDto>(HttpMethod.Post, $"api/members/{id}/reset-link", null, "reset-link", ct);

    public Task<ApiResult<bool>> ResetPasswordAsync(
        string token, NewPasswordDto request, CancellationToken ct = default)
        => _http.SendAsync<bool>(
            HttpMethod.Post, $"api/password-reset/{Uri.EscapeDataString(token)}", request, "password-reset", ct);

    public Task<ApiResult<EmergencyCodeDto>> EmergencyResetAsync(
        EmergencyResetDto request, CancellationToken ct = default)
        => _http.SendAsync<EmergencyCodeDto>(HttpMethod.Post, "api/emergency-reset", request, "emergency-reset", ct);
}
