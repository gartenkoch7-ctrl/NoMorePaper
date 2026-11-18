using ICloudSpike.Http;
using ICloudSpike.Terminal;

namespace ICloudSpike.Auth;

/// <summary>Asks the user for the verification code on the console and has Apple check it.</summary>
internal sealed class TwoFactorDialogue
{
    // Few tries on purpose: repeated wrong codes can lock the Apple-ID for a while.
    private const int MaxCodeAttempts = 3;
    private const int MaxInputs = 12;
    private const int CodeLength = 6;

    private readonly IUserConsole _console;
    private readonly VerificationCodeClient _codes;

    public TwoFactorDialogue(IUserConsole console, VerificationCodeClient codes)
    {
        _console = console;
        _codes = codes;
    }

    /// <summary>Returns once the session is trusted; throws if the user gives up or runs out of tries.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var phones = await _codes.GetTrustedPhonesAsync(cancellationToken);
        await RequestDeviceCodeAsync(cancellationToken);
        _console.WriteLine("Eingaben: sechsstelliger Code, „s“ für Code per SMS, „n“ für neuen Code, leer für Abbruch.");

        TrustedPhone? smsPhone = null;
        var attempts = 0;
        for (var input = 0; input < MaxInputs && attempts < MaxCodeAttempts; input++)
        {
            var entry = _console.ReadLine("Bestätigungscode: ").Replace(" ", string.Empty, StringComparison.Ordinal);
            switch (entry.ToUpperInvariant())
            {
                case "":
                    throw new ICloudException(ICloudFailure.Cancelled, "2FA: cancelled by the user.");
                case "N":
                    smsPhone = null;
                    await RequestDeviceCodeAsync(cancellationToken);
                    continue;
                case "S":
                    smsPhone = await RequestSmsAsync(phones, cancellationToken);
                    continue;
            }

            if (entry.Length != CodeLength || !entry.All(char.IsAsciiDigit))
            {
                _console.WriteLine("Das ist kein sechsstelliger Code.");
                continue;
            }

            attempts++;
            var accepted = smsPhone is null
                ? await _codes.VerifyDeviceCodeAsync(entry, cancellationToken)
                : await _codes.VerifySmsCodeAsync(smsPhone.Id, entry, cancellationToken);
            if (accepted)
            {
                await _codes.TrustSessionAsync(cancellationToken);
                _console.WriteLine("Code bestätigt.");
                return;
            }

            _console.WriteLine($"Apple hat den Code abgelehnt (Versuch {attempts} von {MaxCodeAttempts}).");
        }

        throw new ICloudException(ICloudFailure.TwoFactorFailed, "2FA: no accepted code.");
    }

    private async Task RequestDeviceCodeAsync(CancellationToken cancellationToken)
    {
        var requested = await _codes.RequestDeviceCodeAsync(cancellationToken);
        _console.WriteLine(requested
            ? "Apple zeigt jetzt auf deinen Apple-Geräten eine Anmeldeanfrage. Dort „Erlauben“ wählen, dann erscheint der Code."
            : "Apple hat die Code-Anforderung nicht angenommen. Mit „s“ einen Code per SMS anfordern oder mit „n“ erneut versuchen.");
    }

    private async Task<TrustedPhone?> RequestSmsAsync(
        IReadOnlyList<TrustedPhone> phones, CancellationToken cancellationToken)
    {
        if (phones.Count == 0)
        {
            _console.WriteLine("Apple hat keine Telefonnummer für SMS genannt.");
            return null;
        }

        var phone = phones[0];
        if (phones.Count > 1)
        {
            for (var index = 0; index < phones.Count; index++)
            {
                _console.WriteLine($"  {index + 1}: {phones[index].MaskedNumber}");
            }

            var choice = _console.ReadLine("Nummer der Telefonnummer: ");
            if (!int.TryParse(choice, out var number) || number < 1 || number > phones.Count)
            {
                _console.WriteLine("Keine gültige Auswahl.");
                return null;
            }

            phone = phones[number - 1];
        }

        await _codes.RequestSmsCodeAsync(phone.Id, cancellationToken);
        _console.WriteLine($"SMS angefordert an {phone.MaskedNumber}.");
        return phone;
    }
}
