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

    public AccountApi(HttpClient http, IStringLocalizer<Strings> localizer, ILogger<AccountApi> logger)
        => _http = new AccountHttp(http, localizer, logger);

    public Task<ApiResult<SetupStatusDto>> GetSetupAsync(CancellationToken ct = default)
        => _http.SendAsync<SetupStatusDto>(HttpMethod.Get, "api/setup", null, "setup-status", ct);

    public Task<ApiResult<EmergencyCodeDto>> SetUpAsync(SetupRequestDto request, CancellationToken ct = default)
        => _http.SendAsync<EmergencyCodeDto>(HttpMethod.Post, "api/setup", request, "setup", ct);

    public Task<ApiResult<bool>> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
        => _http.SendAsync<bool>(HttpMethod.Post, "api/auth/login", request, "login", ct);

    public Task<ApiResult<bool>> LogoutAsync(bool everywhere, CancellationToken ct = default)
        => _http.SendAsync<bool>(
            HttpMethod.Post, everywhere ? "api/auth/logout-everywhere" : "api/auth/logout", null, "logout", ct);

    public Task<ApiResult<MeDto>> GetMeAsync(CancellationToken ct = default)
        => _http.SendAsync<MeDto>(HttpMethod.Get, "api/auth/me", null, "me", ct);

    public Task<ApiResult<bool>> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken ct = default)
        => _http.SendAsync<bool>(HttpMethod.Post, "api/account/password", request, "change-password", ct);

    public Task<ApiResult<InvitationLinkDto>> InviteAsync(CancellationToken ct = default)
        => _http.SendAsync<InvitationLinkDto>(HttpMethod.Post, "api/invitations", null, "invite", ct);

    public Task<ApiResult<InvitationStatusDto>> GetInvitationAsync(string token, CancellationToken ct = default)
        => _http.SendAsync<InvitationStatusDto>(
            HttpMethod.Get, $"api/invitations/{Uri.EscapeDataString(token)}", null, "invitation", ct);

    public Task<ApiResult<bool>> AcceptInvitationAsync(
        string token, AcceptInvitationRequestDto request, CancellationToken ct = default)
        => _http.SendAsync<bool>(
            HttpMethod.Post, $"api/invitations/{Uri.EscapeDataString(token)}/accept", request, "accept", ct);

    public Task<ApiResult<MemberDto[]>> ListMembersAsync(CancellationToken ct = default)
        => _http.SendAsync<MemberDto[]>(HttpMethod.Get, "api/members", null, "members", ct);

    public Task<ApiResult<bool>> RemoveMemberAsync(Guid id, CancellationToken ct = default)
        => _http.SendAsync<bool>(HttpMethod.Delete, $"api/members/{id}", null, "remove-member", ct);

    public Task<ApiResult<InvitationLinkDto>> CreateResetLinkAsync(Guid id, CancellationToken ct = default)
        => _http.SendAsync<InvitationLinkDto>(HttpMethod.Post, $"api/members/{id}/reset-link", null, "reset-link", ct);

    public Task<ApiResult<bool>> ResetPasswordAsync(
        string token, ResetPasswordRequestDto request, CancellationToken ct = default)
        => _http.SendAsync<bool>(
            HttpMethod.Post, $"api/password-reset/{Uri.EscapeDataString(token)}", request, "password-reset", ct);

    public Task<ApiResult<EmergencyCodeDto>> EmergencyResetAsync(
        EmergencyResetRequestDto request, CancellationToken ct = default)
        => _http.SendAsync<EmergencyCodeDto>(HttpMethod.Post, "api/emergency-reset", request, "emergency-reset", ct);
}
