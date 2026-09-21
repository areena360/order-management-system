using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using OMS_Backend.Data;
using OMS_Backend.Models;
namespace OMS_Backend.Services;

public class ShopifyApi(IHttpClientFactory clients,IDataProtectionProvider protection,IConfiguration config,OMSDbContext db) {
    public const string Version="2026-07";
    // Keep the base installation limited to scopes that don't require a separate
    // Shopify API access request. Fulfillment access can be added later after
    // Shopify approves the app for those restricted scopes.
    public const string Scopes="read_orders,read_products,read_customers";
    private IDataProtector Protector => protection.CreateProtector("OMS.Shopify.OfflineToken.v1");
    public async Task SetToken(ShopifyStore store,JsonElement token) {
        var granted=token.TryGetProperty("scope",out var scopeValue)?scopeValue.GetString()??"":store.Scopes;
        var permissions=granted.Split(',').ToHashSet(StringComparer.Ordinal);
        if(!permissions.Contains("read_orders")&&!permissions.Contains("write_orders"))throw new InvalidOperationException("Shopify order permission missing; reinstall with the configured scopes.");
        if(!permissions.Contains("read_products")&&!permissions.Contains("write_products"))throw new InvalidOperationException("Shopify product permission missing; reinstall with the configured scopes.");
        if(!permissions.Contains("read_customers")&&!permissions.Contains("write_customers"))throw new InvalidOperationException("Shopify customer permission missing; reinstall with the configured scopes.");
        store.ProtectedToken=Protector.Protect(token.GetProperty("access_token").GetString()!);
        if(token.TryGetProperty("refresh_token",out var refresh)) store.ProtectedRefreshToken=Protector.Protect(refresh.GetString()!);
        store.TokenExpiresAt=token.TryGetProperty("expires_in",out var expiry)?DateTime.UtcNow.AddSeconds(expiry.GetDouble()):null;
        if(token.TryGetProperty("scope",out var scopes)) store.Scopes=scopes.GetString()??"";
        await db.SaveChangesAsync();
    }
    public async Task<JsonElement> Token(string shop,object body) {
        if(!ShopifySecurity.ValidShop(shop)) throw new InvalidOperationException("Invalid Shopify domain.");
        var fields=JsonSerializer.SerializeToElement(body).EnumerateObject()
            .Where(x=>x.Value.ValueKind!=JsonValueKind.Null)
            .ToDictionary(x=>x.Name,x=>x.Value.ToString());
        using var response=await clients.CreateClient("shopify").PostAsync(
            $"https://{shop}/admin/oauth/access_token",new FormUrlEncodedContent(fields));
        if(!response.IsSuccessStatusCode) {
            var detail=(await response.Content.ReadAsStringAsync()).Replace("\r"," ").Replace("\n"," ").Trim();
            throw new InvalidOperationException($"Shopify token exchange returned {(int)response.StatusCode}{(detail.Length==0?"":": "+detail[..Math.Min(detail.Length,400)])}");
        }
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }
    public async Task<JsonElement> Query(ShopifyStore store,string query,object? variables=null) {
        if(!ShopifySecurity.ValidShop(store.Shop)||!store.Connection.IsActive) throw new InvalidOperationException("Shopify connection inactive.");
        if(store.TokenExpiresAt.HasValue && store.TokenExpiresAt<DateTime.UtcNow.AddMinutes(5)) {
            if(store.ProtectedRefreshToken=="") throw new InvalidOperationException("Shopify token expired; reconnect the store.");
            var token=await Token(store.Shop,new{client_id=config["Shopify:ClientId"],client_secret=config["Shopify:ClientSecret"],grant_type="refresh_token",refresh_token=Protector.Unprotect(store.ProtectedRefreshToken)});
            await SetToken(store,token);
        }
        using var request=new HttpRequestMessage(HttpMethod.Post,$"https://{store.Shop}/admin/api/{Version}/graphql.json");
        request.Headers.Add("X-Shopify-Access-Token",Protector.Unprotect(store.ProtectedToken));
        request.Content=JsonContent.Create(new{query,variables});
        using var response=await clients.CreateClient("shopify").SendAsync(request);
        if(!response.IsSuccessStatusCode) throw new InvalidOperationException($"Shopify API returned {(int)response.StatusCode}; verify app access and retry.");
        var result=await response.Content.ReadFromJsonAsync<JsonElement>();
        if(result.TryGetProperty("errors",out var errors)) {
            var details=string.Join(" | ",errors.EnumerateArray()
                .Select(x=>x.TryGetProperty("message",out var message)?message.GetString():null)
                .Where(x=>!string.IsNullOrWhiteSpace(x))
                .Select(x=>x!.Replace("\r"," ").Replace("\n"," "))
                .Take(3));
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(details)
                ? "Shopify GraphQL request failed; verify scopes, protected customer data access and API limits."
                : $"Shopify GraphQL: {details[..Math.Min(details.Length,500)]}");
        }
        return result.GetProperty("data").Clone();
    }
    public static void CheckMutation(JsonElement value) {
        if(value.TryGetProperty("userErrors",out var errors) && errors.GetArrayLength()>0)
            throw new InvalidOperationException("Shopify rejected the update; check fulfillment permissions, location and order state.");
    }
}
