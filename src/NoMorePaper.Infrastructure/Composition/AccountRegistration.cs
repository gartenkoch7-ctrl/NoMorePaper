using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Infrastructure.Accounts;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Composition;

/// <summary>
/// The account store for the Server: ASP.NET Identity's user management (no roles, no sign-in
/// manager; the Server issues its own cookie) with the rules of <see cref="AccountRules"/>.
/// </summary>
public static class AccountRegistration
{
    public static IServiceCollection AddAccountStore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddIdentityCore<AccountUser>(options =>
            {
                // Only a minimum length (plan M2): long passphrases beat character-class rules.
                options.Password.RequiredLength = AccountRules.MinPasswordLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 0;

                options.Lockout.MaxFailedAccessAttempts = AccountRules.MaxFailedSignIns;
                options.Lockout.DefaultLockoutTimeSpan = AccountRules.LockoutDuration;
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = false;
            })
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddScoped<IAccountStore, AccountStore>();
        return services;
    }
}
