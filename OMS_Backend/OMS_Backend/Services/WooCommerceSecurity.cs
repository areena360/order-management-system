using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OMS_Backend.Data;
using OMS_Backend.Models;

namespace OMS_Backend.Services;

public static class WooCommerceSecurity
{
    public const string Scheme = "WooCommerce";
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Secret() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    public static bool Matches(string secret, string hash) => hash.Length == 64 &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Hash(secret)), Encoding.UTF8.GetBytes(hash));
    public static bool Enabled(IConfiguration c) => c.GetValue<bool>("WooCommerce:Enabled");
    public static bool LocalDevelopment(IConfiguration c, IHostEnvironment env) => env.IsDevelopment() && c.GetValue<bool>("WooCommerce:AllowLocalHttp");
    public static bool LoopbackUrl(Uri uri) => new[] { "localhost", "127.0.0.1", "[::1]", "::1" }.Contains(uri.Host);
    public static bool TransportAllowed(HttpRequest request, IConfiguration c, IHostEnvironment env) => request.IsHttps ||
        (LocalDevelopment(c, env) && request.HttpContext.Connection.RemoteIpAddress is { } ip && System.Net.IPAddress.IsLoopback(ip) &&
         new[] { "localhost", "127.0.0.1", "[::1]", "::1" }.Contains(request.Host.Host));
}

public class WooCommerceAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    OMSDbContext db, IConfiguration config, IHostEnvironment environment) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!WooCommerceSecurity.Enabled(config)) return AuthenticateResult.Fail("Integration disabled.");
        if (!WooCommerceSecurity.TransportAllowed(Request, config, environment)) return AuthenticateResult.Fail("HTTPS required.");
        var token = Request.Headers.Authorization.ToString();
        if (!token.StartsWith("Bearer ", StringComparison.Ordinal) || token.Length != 71 ||
            !int.TryParse(Request.Headers["X-OMS-Connection"], out var id))
            return AuthenticateResult.Fail("Invalid installation credential.");
        var c = await db.Set<WooCommerceConnection>().AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == id && x.Provider == "WooCommerce" && x.IsActive && x.OwnerUser.IsActive && !x.OwnerUser.IsDeleted && x.OwnerUser.RoleId == 4);
        if (c == null || !WooCommerceSecurity.Matches(token[7..], c.AccessTokenHash))
            return AuthenticateResult.Fail("Invalid installation credential.");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim("connectionId", c.Id.ToString()), new Claim("ownerId", c.OwnerUserId.ToString())
        }, WooCommerceSecurity.Scheme));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, WooCommerceSecurity.Scheme));
    }
}
