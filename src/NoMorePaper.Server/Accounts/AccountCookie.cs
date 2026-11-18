using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Server.Accounts;

/// <summary>
/// Cookie sign-in of the API (ADR-0021): <c>HttpOnly</c>, <c>SameSite=Strict</c>, <c>Secure</c>, 30 days
/// sliding, checked against the store every 5 minutes. API routes answer 401/403 as ProblemDetails
/// instead of redirecting to a login page.
/// </summary>
internal static class AccountCookie
{
    /// <summary>The <c>__Host-</c> prefix makes browsers insist on Secure, path / and no domain.</summary>
    public const string Name = "__Host-nmp";

    /// <summary>
    /// Development serves plain HTTP (tests, local runs), where browsers drop a <c>__Host-</c> cookie
    /// that is not Secure; there the cookie is named without the prefix.
    /// </summary>
    public const string DevelopmentName = "nmp";

    public static IServiceCollection AddServerAccounts(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentAccount, HttpCurrentAccount>();
        services.AddScoped<SessionValidator>();
        services.AddScoped<AccountSessions>();
        services.AddOptions<AccountSessionOptions>();
        services.AddAuthorization();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IHostEnvironment>(Configure);
        return services;
    }

    private static void Configure(CookieAuthenticationOptions options, IHostEnvironment environment)
    {
        var development = environment.IsDevelopment();
        options.Cookie.Name = development ? DevelopmentName : Name;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = AccountSessionOptions.Lifetime;
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = context =>
                context.HttpContext.RequestServices.GetRequiredService<SessionValidator>().ValidateAsync(context),
            OnRedirectToLogin = context => AccountResponses.SignInRequired().ExecuteAsync(context.HttpContext),
            OnRedirectToAccessDenied = context =>
                AccountResponses.Problem(StatusCodes.Status403Forbidden, "Not allowed.").ExecuteAsync(context.HttpContext),
        };
    }
}
