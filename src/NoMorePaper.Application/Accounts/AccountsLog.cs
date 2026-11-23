using Microsoft.Extensions.Logging;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Accounts;

/// <summary>
/// Log events of the account use cases (Application 5100–5119, docs/kb/telemetrie.md). Account ids
/// only: never user names, display names, passwords, link tokens or emergency codes.
/// </summary>
internal static partial class AccountsLog
{
    [LoggerMessage(EventId = 5100, Level = LogLevel.Information, Message = "Household set up with administrator {AccountId}; {AdoptedDocuments} documents without owner handed to it")]
    public static partial void HouseholdSetUp(this ILogger logger, Guid accountId, int adoptedDocuments);

    [LoggerMessage(EventId = 5101, Level = LogLevel.Warning, Message = "Setup refused: the household is set up already")]
    public static partial void SetupRefused(this ILogger logger);

    [LoggerMessage(EventId = 5102, Level = LogLevel.Information, Message = "Account {AccountId} created an invitation valid until {ExpiresAt}")]
    public static partial void InvitationCreated(this ILogger logger, Guid accountId, DateTimeOffset expiresAt);

    [LoggerMessage(EventId = 5103, Level = LogLevel.Information, Message = "Invitation accepted: new member {AccountId}")]
    public static partial void InvitationAccepted(this ILogger logger, Guid accountId);

    [LoggerMessage(EventId = 5104, Level = LogLevel.Information, Message = "Account {AccountId} removed member {MemberId}")]
    public static partial void MemberRemoved(this ILogger logger, Guid accountId, Guid memberId);

    [LoggerMessage(EventId = 5105, Level = LogLevel.Information, Message = "Account {AccountId} created a reset link for member {MemberId}, valid until {ExpiresAt}")]
    public static partial void ResetLinkCreated(this ILogger logger, Guid accountId, Guid memberId, DateTimeOffset expiresAt);

    [LoggerMessage(EventId = 5106, Level = LogLevel.Information, Message = "Password of account {AccountId} reset with a reset link")]
    public static partial void PasswordReset(this ILogger logger, Guid accountId);

    [LoggerMessage(EventId = 5107, Level = LogLevel.Warning, Message = "Password of account {AccountId} reset with the emergency code; a new code was issued")]
    public static partial void EmergencyReset(this ILogger logger, Guid accountId);

    [LoggerMessage(EventId = 5108, Level = LogLevel.Warning, Message = "Emergency reset refused: unknown account or wrong code")]
    public static partial void EmergencyResetRefused(this ILogger logger);

    [LoggerMessage(EventId = 5109, Level = LogLevel.Information, Message = "Account {AccountId} changed its password")]
    public static partial void PasswordChanged(this ILogger logger, Guid accountId);

    [LoggerMessage(EventId = 5110, Level = LogLevel.Information, Message = "Account {AccountId} ended all its sessions")]
    public static partial void SignedOutEverywhere(this ILogger logger, Guid accountId);

    [LoggerMessage(EventId = 5111, Level = LogLevel.Warning, Message = "Account {AccountId} is not the administrator and was refused: {Action}")]
    public static partial void AdministratorOnly(this ILogger logger, Guid accountId, string action);

    [LoggerMessage(EventId = 5112, Level = LogLevel.Information, Message = "Account {AccountId} acknowledged that its password was reset with a reset link")]
    public static partial void AdminResetAcknowledged(this ILogger logger, Guid accountId);

    [LoggerMessage(EventId = 5113, Level = LogLevel.Warning, Message = "Account {AccountId} no longer exists; its session was refused: {Action}")]
    public static partial void AccountGone(this ILogger logger, Guid accountId, string action);

    [LoggerMessage(EventId = 5114, Level = LogLevel.Information, Message = "Account {AccountId} now keeps {Policy} after editing a document")]
    public static partial void AfterEditingChanged(this ILogger logger, Guid accountId, VersionPolicy policy);
}
