using System.Text.Json.Nodes;
using ICloudSpike.Http;
using ICloudSpike.Session;

namespace ICloudSpike.Auth;

/// <summary>
/// The iCloud part of the sign-in (host setup.icloud.com): turns Apple's session token into iCloud
/// cookies and reports which services the account offers.
/// </summary>
internal sealed class SetupClient
{
    private const string SetupUrl = "https://setup.icloud.com/setup/ws/1";

    // Version stamps of the iCloud web app that the open-source clients currently send.
    private const string ClientBuildNumber = "2534Project66";
    private const string ClientMasteringNumber = "2534B22";

    private readonly ICloudTransport _transport;
    private readonly SessionData _session;

    public SetupClient(ICloudTransport transport, SessionData session)
    {
        _transport = transport;
        _session = session;
    }

    /// <summary>Query parameters every iCloud service call carries.</summary>
    public static Dictionary<string, string> ServiceQuery(string clientId, string? dsid)
    {
        var query = new Dictionary<string, string>
        {
            ["clientBuildNumber"] = ClientBuildNumber,
            ["clientMasteringNumber"] = ClientMasteringNumber,
            ["clientId"] = clientId,
        };
        if (dsid is not null)
        {
            query["dsid"] = dsid;
        }

        return query;
    }

    /// <summary>
    /// Checks whether the stored cookies are still accepted. Null means "no"; the caller then tries
    /// the session token and after that the password.
    /// </summary>
    public async Task<AccountState?> ValidateAsync(CancellationToken cancellationToken)
    {
        var uri = ApiRequest.BuildUri(SetupUrl + "/validate", ServiceQuery(_session.ClientId, dsid: null));
        var response = await _transport.SendAsync(
            new ApiRequest(HttpMethod.Post, uri) { Body = "null", ContentType = ApiRequest.PlainTextContent },
            cancellationToken);
        return response.IsSuccess && response.Json is { } json ? AccountState.Parse(json) : null;
    }

    /// <summary>Exchanges the session token for iCloud cookies.</summary>
    public async Task<AccountState> AccountLoginAsync(CancellationToken cancellationToken)
    {
        if (_session.SessionToken is null)
        {
            throw new ICloudException(ICloudFailure.SessionExpired, "accountLogin: no session token stored.");
        }

        var body = new JsonObject
        {
            ["accountCountryCode"] = _session.AccountCountry,
            ["dsWebAuthToken"] = _session.SessionToken,
            ["extended_login"] = true,
            ["trustToken"] = _session.TrustToken ?? string.Empty,
        };
        var uri = ApiRequest.BuildUri(SetupUrl + "/accountLogin", ServiceQuery(_session.ClientId, dsid: null));
        var response = await _transport.SendAsync(
            new ApiRequest(HttpMethod.Post, uri) { Body = body.ToJsonString() }, cancellationToken);
        if (response.Status is 401 or 421 or 450)
        {
            throw ResponseErrors.Create("accountLogin", response, ICloudFailure.SessionExpired);
        }

        if (!response.IsSuccess || response.Json is not { } json)
        {
            throw ResponseErrors.Create("accountLogin", response);
        }

        var state = AccountState.Parse(json);
        if (state.DomainToUse is not null)
        {
            throw new ICloudException(
                ICloudFailure.Unexpected,
                "accountLogin: the account lives on another iCloud domain (e.g. mainland China); not covered by the spike.");
        }

        return state;
    }
}
