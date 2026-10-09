using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace OMS_Backend.Services;
public static class ShopifySecurity {
    public static bool ValidShop(string shop) => Regex.IsMatch(shop, "^[a-z0-9][a-z0-9-]*\\.myshopify\\.com$", RegexOptions.CultureInvariant) && shop.Length<=255;
    public static bool VerifyBody(byte[] body,string signature,string secret) {
        if(string.IsNullOrWhiteSpace(secret)) return false;
        Span<byte> decoded = stackalloc byte[32];
        return Convert.TryFromBase64String(signature,decoded,out var written) && written==32 &&
            CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),body),decoded);
    }
    public static bool VerifyQuery(IQueryCollection query,string secret) {
        if(string.IsNullOrWhiteSpace(secret)||query.Any(x=>x.Value.Count!=1)) return false;
        var message=string.Join("&",query.Where(x=>x.Key!="hmac" && x.Key!="signature").OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>$"{x.Key}={x.Value}"));
        var signature=query["hmac"].ToString();
        return signature.Length==64 && signature.All(Uri.IsHexDigit) &&
            CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),Encoding.UTF8.GetBytes(message)),Convert.FromHexString(signature));
    }
    public static string PublicUrl(IConfiguration c,string key) {
        var value=c[$"Shopify:{key}"]?.TrimEnd('/') ?? "";
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo!=""||uri.Query!=""||uri.Fragment!="")
            throw new InvalidOperationException($"Configure Shopify:{key} with a public HTTPS URL.");
        return value;
    }
}
