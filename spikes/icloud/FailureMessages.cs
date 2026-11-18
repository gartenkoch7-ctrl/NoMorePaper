using ICloudSpike.Auth;
using ICloudSpike.Http;

namespace ICloudSpike;

/// <summary>German explanations for the user: what happened and what to do next.</summary>
internal static class FailureMessages
{
    public static string Describe(ICloudFailure failure) => failure switch
    {
        ICloudFailure.WrongCredentials =>
            "Apple hat Apple-ID oder Passwort abgelehnt. Bitte nur EINMAL erneut versuchen – mehrere "
            + "Fehlversuche können die Apple-ID sperren. Stimmt das Passwort sicher, ist das ein Befund für das ADR.",
        ICloudFailure.AccountLocked =>
            "Apple meldet die Apple-ID als gesperrt. Bitte unter https://iforgot.apple.com entsperren; "
            + "den Spike vorher nicht erneut starten.",
        ICloudFailure.RepairRequired =>
            "Apple verlangt zuerst eine Bestätigung im Browser (z. B. neue Bedingungen). Bitte einmal "
            + "unter https://www.icloud.com anmelden, dort alles bestätigen, dann den Spike neu starten.",
        ICloudFailure.RateLimited =>
            "Apple bremst gerade (HTTP 429 oder 503). Mindestens 30 Minuten warten, dann erneut versuchen. "
            + "Nicht in kurzer Folge wiederholen.",
        ICloudFailure.SessionExpired =>
            "Die gespeicherte Sitzung wird nicht mehr angenommen. Den Spike neu starten; er meldet sich "
            + "dann neu an. Hilft das nicht: mit --logout die Sitzungsdatei löschen.",
        ICloudFailure.TwoFactorFailed =>
            "Die Bestätigung mit Code ist nicht gelungen. Kam kein Code an, ist das ein wichtiger Befund "
            + "(siehe README, Abschnitt „Wenn kein Code kommt“).",
        ICloudFailure.WebAccessBlocked =>
            "Das Konto erlaubt keinen Web-Zugriff auf iCloud Drive. Ursache ist meist „Erweiterter "
            + "Datenschutz“ (Advanced Data Protection) mit abgeschaltetem Web-Zugriff.",
        ICloudFailure.ProtocolChanged =>
            "Apples Antwort passt nicht mehr zum bekannten Ablauf. Apple hat das Protokoll vermutlich "
            + "geändert – Befund für das ADR.",
        ICloudFailure.Network =>
            "Keine Verbindung zu Apple. Internetverbindung prüfen und erneut versuchen.",
        ICloudFailure.Cancelled => "Abgebrochen.",
        _ => "Unerwartete Antwort von Apple – Befund für das ADR.",
    };

    public static string Describe(LoginPath path) => path switch
    {
        LoginPath.ReusedSession => "gespeicherte Sitzung wiederverwendet, keine Eingabe nötig",
        LoginPath.RenewedByToken => "Sitzung mit gespeichertem Token erneuert, keine Eingabe nötig",
        LoginPath.PasswordOnly => "Passwort nötig, aber kein Code (Gerät war noch vertraut)",
        _ => "Passwort und Bestätigungscode",
    };

    public static string YesNo(bool? value) => value switch
    {
        true => "ja",
        false => "nein",
        null => "unbekannt",
    };
}
