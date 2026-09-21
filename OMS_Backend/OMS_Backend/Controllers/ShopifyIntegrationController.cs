using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Common.Exceptions;
using OMS_Backend.Data;
using OMS_Backend.Models;
using OMS_Backend.Services;
namespace OMS_Backend.Controllers;

public sealed class ShopifyEnabledAttribute : Attribute,IAsyncResourceFilter {
    public async Task OnResourceExecutionAsync(ResourceExecutingContext c,ResourceExecutionDelegate next) {
        if(!c.HttpContext.RequestServices.GetRequiredService<IConfiguration>().GetValue<bool>("Shopify:Enabled")) { c.Result=new NotFoundResult(); return; }
        await next();
    }
}
public record ShopifyConnectDto(string Shop,int? OwnerUserId,int GenderId,int MaterialId,int StatusId);
public record ShopifySettingsDto(int GenderId,int MaterialId,int StatusId,bool FulfillmentEnabled,int? ShippedStatusId);
[ApiController,Route("api/integrations/shopify"),ShopifyEnabled]
public class ShopifyIntegrationController(OMSDbContext db,ShopifyApi api,IConfiguration config,WooCommerceIntegrationService importer) : ControllerBase {
    private int UserId=>int.TryParse(User.FindFirstValue("userId"),out var n)?n:0;
    private bool Admin=>User.IsInRole("Admin")||User.IsInRole("Super Admin");
    private async Task Manager() {
        if(!await db.Users.AnyAsync(x=>x.Id==UserId && x.IsActive && !x.IsDeleted && (Admin?(x.RoleId==1||x.RoleId==2):x.RoleId==4))) throw new ForbiddenAppException();
    }
    private async Task<ShopifyStore> Managed(int id) {
        await Manager(); return await db.Set<ShopifyStore>().Include(x=>x.Connection).SingleOrDefaultAsync(x=>x.Id==id && (Admin||x.Connection.OwnerUserId==UserId)) ?? throw new NotFoundException("Store",id);
    }
    [Authorize,HttpGet("configuration")]
    public async Task<IActionResult> Configuration() { await Manager(); var direct=config.GetValue<bool>("Shopify:LocalDirectMode"); return Ok(new { ready=!string.IsNullOrEmpty(config["Shopify:ClientId"])&&!string.IsNullOrEmpty(config["Shopify:ClientSecret"])&&(direct||!string.IsNullOrEmpty(config["Shopify:PublicBaseUrl"])), frontendUrl=direct?null:config["Shopify:FrontendUrl"], localDirectMode=direct, apiVersion=ShopifyApi.Version }); }
    [Authorize,HttpGet("stores")]
    public async Task<IActionResult> Stores() {
        await Manager(); return Ok(await db.Set<ShopifyStore>().Where(x=>Admin||x.Connection.OwnerUserId==UserId).Select(x=>new{x.Id,x.Shop,x.Connection.IsActive,x.Connection.LastSyncAt,x.Connection.DefaultGenderId,x.Connection.DefaultMaterialId,x.Connection.DefaultStatusId,x.FulfillmentEnabled,x.ShippedStatusId,x.LastError,orderCount=db.Set<WooCommerceOrder>().Count(w=>w.ConnectionId==x.ConnectionId)}).ToListAsync());
    }
    [Authorize,HttpGet("owners")]
    public async Task<IActionResult> Owners() { await Manager(); if(!Admin) return Forbid(); return Ok(await db.Users.Where(x=>x.RoleId==4&&x.IsActive&&!x.IsDeleted).Select(x=>new{x.Id,name=x.FirstName+" "+x.LastName,x.Email}).ToListAsync()); }
    [Authorize,HttpPost("connect"),EnableRateLimiting("woo")]
    public async Task<IActionResult> Connect(ShopifyConnectDto dto) {
        await Manager(); var shop=dto.Shop.Trim().ToLowerInvariant(); if(!ShopifySecurity.ValidShop(shop)) return BadRequest(new{message="Enter your store.myshopify.com domain only."});
        var owner=Admin?dto.OwnerUserId??0:UserId;
        if(!await db.Users.AnyAsync(x=>x.Id==owner&&x.RoleId==4&&x.IsActive&&!x.IsDeleted)) return BadRequest(new{message="Select an active Customer account."});
        if(await db.Set<ShopifyStore>().AnyAsync(x=>x.Shop==shop&&x.Connection.OwnerUserId!=owner)) return Conflict(new{message="This store belongs to a different OMS customer."});
        await importer.ValidateDefaults(dto.GenderId,dto.MaterialId,dto.StatusId);
        var direct=config.GetValue<bool>("Shopify:LocalDirectMode");
        if(string.IsNullOrWhiteSpace(config["Shopify:ClientId"])||string.IsNullOrWhiteSpace(config["Shopify:ClientSecret"])) return BadRequest(new{message="Configure Shopify app credentials on the server first."});
        if(direct) {
            var token=await api.Token(shop,new {client_id=config["Shopify:ClientId"],client_secret=config["Shopify:ClientSecret"],grant_type="client_credentials"});
            var store=await db.Set<ShopifyStore>().Include(x=>x.Connection).SingleOrDefaultAsync(x=>x.Shop==shop);
            if(store==null) { store=new ShopifyStore {Shop=shop,Connection=new WooCommerceConnection {Provider="Shopify",InstallationId=Guid.NewGuid(),OwnerUserId=owner,StoreName=shop,StoreUrl="https://"+shop,AccessTokenHash=WooCommerceSecurity.Hash(WooCommerceSecurity.Secret())}}; db.Add(store); }
            store.Connection.IsActive=true;store.Connection.DefaultGenderId=dto.GenderId;store.Connection.DefaultMaterialId=dto.MaterialId;store.Connection.DefaultStatusId=dto.StatusId;store.NextPollAt=DateTime.UtcNow;store.LastError="";
            await api.SetToken(store,token);
            return Ok(new {direct=true});
        }
        var callback=ShopifySecurity.PublicUrl(config,"PublicBaseUrl")+"/api/integrations/shopify/callback";
        var publicOrigin=new Uri(ShopifySecurity.PublicUrl(config,"PublicBaseUrl")).GetLeftPart(UriPartial.Authority);
        if(Request.Headers.Origin.ToString()!=publicOrigin) return BadRequest(new{message="Open the configured public HTTPS OMS address and start the connection there, so authorization stays bound to this browser."});
        var state=WooCommerceSecurity.Secret();
        db.Add(new ShopifyAuthorization {StateHash=WooCommerceSecurity.Hash(state),Shop=shop,OwnerUserId=owner,GenderId=dto.GenderId,MaterialId=dto.MaterialId,StatusId=dto.StatusId,ExpiresAt=DateTime.UtcNow.AddMinutes(10)}); await db.SaveChangesAsync();
        // Bound to this browser in addition to the durable owner-bound nonce.
        Response.Cookies.Append("oms_shopify_state",state,new CookieOptions {HttpOnly=true,Secure=true,SameSite=SameSiteMode.Lax,MaxAge=TimeSpan.FromMinutes(10),Path="/api/integrations/shopify"});
        return Ok(new {url=$"https://{shop}/admin/oauth/authorize?client_id={Uri.EscapeDataString(config["Shopify:ClientId"]!)}&scope={Uri.EscapeDataString(ShopifyApi.Scopes)}&redirect_uri={Uri.EscapeDataString(callback)}&state={state}"});
    }
    [AllowAnonymous,HttpGet("callback"),EnableRateLimiting("woo")]
    public async Task<IActionResult> Callback() {
        if(!ShopifySecurity.VerifyQuery(Request.Query,config["Shopify:ClientSecret"]??"")) return Unauthorized();
        var state=Request.Query["state"].ToString(); var shop=Request.Query["shop"].ToString();
        if(state.Length!=64||!ShopifySecurity.ValidShop(shop)||Request.Cookies["oms_shopify_state"]!=state) return Unauthorized();
        using var tx=await db.Database.BeginTransactionAsync(); await importer.Lock("shopify-oauth:"+shop);
        var pending=await db.Set<ShopifyAuthorization>().SingleOrDefaultAsync(x=>x.StateHash==WooCommerceSecurity.Hash(state)&&x.Shop==shop&&!x.Used&&x.ExpiresAt>DateTime.UtcNow);
        if(pending==null) return BadRequest(new{message="Authorization expired or used. Start again in OMS."});
        if(!await db.Users.AnyAsync(x=>x.Id==pending.OwnerUserId&&x.RoleId==4&&x.IsActive&&!x.IsDeleted)) return Forbid();
        var store=await db.Set<ShopifyStore>().Include(x=>x.Connection).SingleOrDefaultAsync(x=>x.Shop==shop);
        if(store!=null&&store.Connection.OwnerUserId!=pending.OwnerUserId) return Conflict();
        var token=await api.Token(shop,new {client_id=config["Shopify:ClientId"],client_secret=config["Shopify:ClientSecret"],code=Request.Query["code"].ToString(),expiring=1});
        if(store==null) { store=new ShopifyStore {Shop=shop,Connection=new WooCommerceConnection {Provider="Shopify",InstallationId=Guid.NewGuid(),OwnerUserId=pending.OwnerUserId,StoreName=shop,StoreUrl="https://"+shop,AccessTokenHash=WooCommerceSecurity.Hash(WooCommerceSecurity.Secret())}}; db.Add(store); }
        store.Connection.IsActive=true; store.Connection.DefaultGenderId=pending.GenderId;store.Connection.DefaultMaterialId=pending.MaterialId;store.Connection.DefaultStatusId=pending.StatusId;
        store.NextPollAt=DateTime.UtcNow;store.LastError="";pending.Used=true;
        await api.SetToken(store,token); await tx.CommitAsync();
        Response.Cookies.Delete("oms_shopify_state",new CookieOptions{Path="/api/integrations/shopify"});
        return Redirect(ShopifySecurity.PublicUrl(config,"FrontendUrl")+"/dashboard/integrations/shopify?connected=1");
    }
    [Authorize,HttpPut("stores/{id:int}/settings")]
    public async Task<IActionResult> Settings(int id,ShopifySettingsDto dto) {
        var s=await Managed(id);await importer.ValidateDefaults(dto.GenderId,dto.MaterialId,dto.StatusId);
        if(dto.FulfillmentEnabled&&(!dto.ShippedStatusId.HasValue||!await db.LookupItems.AnyAsync(x=>x.Id==dto.ShippedStatusId&&x.LookupDataTypeId==1&&x.IsActive&&!x.IsDeleted))) return BadRequest(new{message="Select the OMS shipped status."});
        s.Connection.DefaultGenderId=dto.GenderId;s.Connection.DefaultMaterialId=dto.MaterialId;s.Connection.DefaultStatusId=dto.StatusId;s.FulfillmentEnabled=dto.FulfillmentEnabled;s.ShippedStatusId=dto.ShippedStatusId;await db.SaveChangesAsync();return NoContent();
    }
    [Authorize,HttpPost("stores/{id:int}/sync")]
    public async Task<IActionResult> Sync(int id,[FromQuery]int days=30) {
        if(days<1||days>60) return BadRequest(new{message="Select 1–60 days. Older order access requires Shopify approval."});
        var s=await Managed(id);s.ScanSince=DateTime.UtcNow.AddDays(-days);s.ScanCursor=null;s.ScanUntil=null;s.NextPollAt=DateTime.UtcNow;await db.SaveChangesAsync();return Accepted();
    }
    [Authorize,HttpPost("stores/{id:int}/retry/{orderId:long}")]
    public async Task<IActionResult> Retry(int id,long orderId) {var s=await Managed(id);if(orderId<=0)return BadRequest();db.Add(new ShopifyJob{StoreId=s.Id,EventId=Guid.NewGuid().ToString(),ExternalId=orderId});await db.SaveChangesAsync();return Accepted();}
    [Authorize,HttpPost("stores/{id:int}/disconnect")]
    public async Task<IActionResult> Disconnect(int id) {var s=await Managed(id);s.Connection.IsActive=false;s.ProtectedToken="";s.ProtectedRefreshToken="";await db.SaveChangesAsync();return NoContent();}
    [Authorize,HttpGet("stores/{id:int}/logs")]
    public async Task<IActionResult> Logs(int id) {var s=await Managed(id);return Ok(await db.Set<ShopifyJob>().Where(x=>x.StoreId==s.Id).OrderByDescending(x=>x.Id).Take(100).Select(x=>new{x.Id,x.Topic,x.ExternalId,x.Attempts,x.DueAt,x.CompletedAt,x.Error}).ToListAsync());}
    [Authorize,HttpGet("stores/{id:int}/privacy/{jobId:long}")]
    public async Task<IActionResult> PrivacyExport(int id,long jobId) {
        var s=await Managed(id);var job=await db.Set<ShopifyJob>().SingleOrDefaultAsync(x=>x.Id==jobId&&x.StoreId==id&&x.Topic=="customers/data_request");
        if(job==null)return NotFound();
        var records=await db.Set<WooCommerceOrder>().Where(x=>x.ConnectionId==s.ConnectionId&&x.Buyer.ExternalCustomerId==job.ExternalId).Select(x=>new{x.ExternalOrderId,x.ExternalLineId,x.UnitNumber,x.ItemsJson,x.BillingJson,x.ShippingJson,x.Buyer.Name,x.Buyer.Email,x.Buyer.Phone,x.Order.NotesByCustomer}).ToListAsync();
        Response.Headers.CacheControl="no-store";
        return File(JsonSerializer.SerializeToUtf8Bytes(new{store=s.Shop,customerId=job.ExternalId,records}),"application/json",$"shopify-privacy-{job.Id}.json");
    }
    [Authorize,HttpPost("stores/{id:int}/privacy/{jobId:long}/resolve")]
    public async Task<IActionResult> ResolvePrivacy(int id,long jobId) {
        await Managed(id);var job=await db.Set<ShopifyJob>().SingleOrDefaultAsync(x=>x.Id==jobId&&x.StoreId==id&&x.Topic=="customers/data_request");
        if(job==null)return NotFound();job.CompletedAt=DateTime.UtcNow;job.Error="Data request resolved by OMS account "+UserId;await db.SaveChangesAsync();return NoContent();
    }
    [Authorize,HttpGet("orders/{orderId:int}")]
    public async Task<IActionResult> Snapshot(int orderId) {
        var link=await db.Set<WooCommerceOrder>().Include(x=>x.Buyer).Include(x=>x.Connection).SingleOrDefaultAsync(x=>x.OrderId==orderId&&x.Connection.Provider=="Shopify"&&!x.Order.IsDeleted);
        if(link==null)return NotFound();var store=await db.Set<ShopifyStore>().SingleAsync(x=>x.ConnectionId==link.ConnectionId);await Managed(store.Id);
        return Ok(new{link.ExternalOrderId,link.ExternalLineId,link.UnitNumber,link.IsCurrentUnit,link.ExternalStatus,link.Currency,link.Total,link.ModifiedAt,link.PaymentMethod,link.ItemsJson,link.BillingJson,link.ShippingJson,link.Connection.StoreName,buyer=new{link.Buyer.Name,link.Buyer.Email,link.Buyer.Phone}});
    }
    [AllowAnonymous,HttpPost("webhooks"),RequestSizeLimit(2097152)]
    public async Task<IActionResult> Webhook() {
        using var buffer=new MemoryStream();await Request.Body.CopyToAsync(buffer);var bytes=buffer.ToArray();
        if(!ShopifySecurity.VerifyBody(bytes,Request.Headers["X-Shopify-Hmac-Sha256"].ToString(),config["Shopify:ClientSecret"]??""))return Unauthorized();
        var shop=Request.Headers["X-Shopify-Shop-Domain"].ToString();var topic=Request.Headers["X-Shopify-Topic"].ToString();
        var store=await db.Set<ShopifyStore>().Include(x=>x.Connection).SingleOrDefaultAsync(x=>x.Shop==shop);if(store==null)return Ok();
        var eventId=Request.Headers["X-Shopify-Event-Id"].ToString();if(eventId=="")eventId=Request.Headers["X-Shopify-Webhook-Id"].ToString();
        if(eventId==""||eventId.Length>100)return BadRequest(); eventId=topic+":"+eventId;
        using var payload=JsonDocument.Parse(bytes);var root=payload.RootElement;
        if(topic=="app/uninstalled") {store.Connection.IsActive=false;store.ProtectedToken="";store.ProtectedRefreshToken="";await db.SaveChangesAsync();return Ok();}
        if(!new[]{"orders/create","orders/updated","orders/cancelled","products/update","customers/data_request","customers/redact","shop/redact"}.Contains(topic))return Ok();
        var externalId=topic.StartsWith("customers/")?ShopifyOrderMapper.Id(ShopifyOrderMapper.Text(root.GetProperty("customer"),"id")):ShopifyOrderMapper.Id(ShopifyOrderMapper.Text(root,"id"));
        if(await db.Set<ShopifyJob>().AnyAsync(x=>x.StoreId==store.Id&&x.EventId==eventId))return Ok();
        db.Add(new ShopifyJob{StoreId=store.Id,EventId=eventId,Topic=topic,ExternalId=externalId});
        try {await db.SaveChangesAsync();} catch(DbUpdateException) {if(!await db.Set<ShopifyJob>().AsNoTracking().AnyAsync(x=>x.StoreId==store.Id&&x.EventId==eventId))throw;}
        return Ok();
    }
}
