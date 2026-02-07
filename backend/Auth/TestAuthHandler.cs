using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Kudos.Api.Auth;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Scheme = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User-Id", out var userId) ||
            string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing test user id"));
        }

        var name = Request.Headers.TryGetValue("X-Test-User-Name", out var userName)
            ? userName.ToString()
            : "Test User";
        var email = Request.Headers.TryGetValue("X-Test-User-Email", out var userEmail)
            ? userEmail.ToString()
            : "test.user@contoso.com";

        var claims = new List<Claim>
        {
            new("oid", userId.ToString()),
            new("name", name),
            new("preferred_username", email)
        };

        var identity = new ClaimsIdentity(claims, Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
