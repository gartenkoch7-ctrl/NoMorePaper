using System.Globalization;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Web.Client.Accessibility;
using NoMorePaper.Web.Client.Accounts;
using NoMorePaper.Web.Client.Inputs;

namespace NoMorePaper.Web.Client.Pages;

/// <summary>
/// <c>/eingaenge/mail</c> (administrator only): server, port, encryption, user name, password and the own
/// mail server's authserv-id of the household's mailbox; save and test the connection.
/// </summary>
public sealed partial class MailSettings
{
    private const string Ssl = "ssl";

    private MailSettingsApi _api = default!;
    private ILogger _logger = default!;
    private FormValidation _form = default!;
    private LiveRegion? _error;
    private LiveRegion? _status;
    private string? _loadError;
    private bool _loading = true;
    private bool _busy;
    private bool _showPassword;
    private bool _hasPassword;
    private string _host = string.Empty;
    private string _port = "993";
    private string _security = Ssl;
    private string _user = string.Empty;
    private string _password = string.Empty;
    private string _authServId = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        // Built here, not registered: the page is its only user.
        _api = new MailSettingsApi(Http, L, Loggers.CreateLogger<MailSettingsApi>());
        _form = new FormValidation(L, Js);
        _logger = Loggers.CreateLogger<MailSettings>();
        await Auth.EnsureLoadedAsync();
        if (Auth.IsAdmin)
        {
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _loadError = null;
        var result = await _api.GetAsync();
        _loading = false;
        if (result.Value is not { } settings)
        {
            _loadError = result.Error;
            return;
        }

        (_host, _port, _security, _user) = (settings.Host, settings.Port.ToString(CultureInfo.InvariantCulture), settings.Security, settings.UserName);
        (_authServId, _hasPassword, _password) = (settings.TrustedAuthServId ?? string.Empty, settings.HasPassword, string.Empty);
        _logger.MailSettingsLoaded(_hasPassword);
    }

    private async Task SaveAsync()
    {
        if (await RequestAsync() is not { } request)
        {
            return;
        }

        _busy = true;
        var result = await _api.SaveAsync(request);
        _busy = false;
        if (!result.IsSuccess)
        {
            _form.Refused(result, "mail-host", (MailSettingsApi.SettingsField, "mail-host"));
            await _form.ReportAsync(_error);
            return;
        }

        var renewed = request.Password is not null;
        _logger.MailSettingsSaved(renewed);
        (_hasPassword, _password) = (_hasPassword || renewed, string.Empty);
        _error?.Clear();
        await (_status?.Announce(L["MailSaved"]) ?? Task.CompletedTask);
    }

    private async Task TestAsync()
    {
        if (await RequestAsync() is not { } request)
        {
            return;
        }

        _busy = true;
        await (_status?.Announce(L["MailTesting"]) ?? Task.CompletedTask);
        var result = await _api.TestAsync(request);
        _busy = false;
        var succeeded = result.IsSuccess;
        _logger.MailSettingsTested(succeeded);
        if (result.IsSuccess)
        {
            _error?.Clear();
            await (_status?.Announce(L["MailTestOk"]) ?? Task.CompletedTask);
            return;
        }

        _status?.Clear();
        await (_error?.Announce(L["ErrorPrefix", result.Error!]) ?? Task.CompletedTask);
    }

    /// <summary>Checks the form; on a problem it is announced and focus moves to the field.</summary>
    private async Task<MailSettingsRequestDto?> RequestAsync()
    {
        _form.Reset();
        _status?.Clear();
        _form.Required("mail-host", _host, L["MailFieldHost"]);
        if (!_form.HasIssue && !(int.TryParse(_port, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= 65535))
        {
            _form.Set("mail-port", L["MailErrorPort"]);
        }

        if (!_form.HasIssue)
        {
            _form.Required("mail-user", _user, L["MailFieldUser"]);
        }

        if (!_form.HasIssue && !_hasPassword)
        {
            _form.Required("mail-password", _password, L["MailFieldPassword"]);
        }

        if (await _form.ReportAsync(_error))
        {
            return null;
        }

        var authServId = string.IsNullOrWhiteSpace(_authServId) ? null : _authServId.Trim();
        var password = _password.Length == 0 ? null : _password;
        return new MailSettingsRequestDto(_host.Trim(), int.Parse(_port, CultureInfo.InvariantCulture), _security, _user.Trim(), authServId, password);
    }
}
