using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Common.Exceptions;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Models;
using OMS_Backend.Services;

namespace OMS_Backend.Controllers;

public sealed class WooEnabledAttribute : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        if (!WooCommerceSecurity.Enabled(config)) { context.Result = new NotFoundResult(); return; }
        var env = context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (!WooCommerceSecurity.TransportAllowed(context.HttpContext.Request, config, env)) { context.Result = new BadRequestObjectResult(new { message = "HTTPS required." }); return; }
        await next();
    }
}

[ApiController, Route("api/integrations/woocommerce"), WooEnabled, EnableRateLimiting("woo")]
[RequestSizeLimit(524288)]
public class WooCommerceIntegrationController(OMSDbContext db, WooCommerceIntegrationService service) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue("userId"), out var id) ? id : 0;
    private bool Admin => User.IsInRole("Super Admin") || User.IsInRole("Admin");
    private int ConnectionId => int.Parse(User.FindFirstValue("connectionId")!);
    private async Task CheckManager()
    {
        if (!(Admin || User.IsInRole("Customer")) || !await db.Users.AnyAsync(x => x.Id == UserId && x.IsActive && !x.IsDeleted &&
            (Admin ? x.RoleId == 1 || x.RoleId == 2 : x.RoleId == 4)))
            throw new ForbiddenAppException();
    }
    private async Task<WooCommerceConnection> Managed(int id)
    {
        await CheckManager();
        return await db.Set<WooCommerceConnection>().FirstOrDefaultAsync(x => x.Id == id && x.Provider == "WooCommerce" && (Admin || x.OwnerUserId == UserId))
            ?? throw new NotFoundException("Connection", id);
    }

    [AllowAnonymous, HttpPost("authorize/start")]
    public async Task<IActionResult> Start(WooStartDto dto)
    {
        if (dto.InstallationId == Guid.Empty || !Uri.TryCreate(dto.StoreUrl, UriKind.Absolute, out var uri) ||
            !AllowedStoreUrl(uri) || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return BadRequest(new { message = "A valid installation ID and HTTPS store URL are required." });
        var code = WooCommerceSecurity.Secret();
        var pending = new WooCommerceAuthorization {
            Id = Guid.NewGuid(), CodeHash = WooCommerceSecurity.Hash(code), TokenHash = dto.TokenHash,
            InstallationId = dto.InstallationId, StoreName = dto.StoreName, StoreUrl = dto.StoreUrl.TrimEnd('/'),
            PluginVersion = dto.PluginVersion, ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        // Only expired authorization attempts are pruned; no imported business data is deleted.
        await db.Set<WooCommerceAuthorization>().Where(x => x.ExpiresAt < DateTime.UtcNow.AddDays(-1)).ExecuteDeleteAsync();
        db.Add(pending);
        await db.SaveChangesAsync();
        Response.Headers.CacheControl = "no-store";
        return Ok(new { deviceId = pending.Id, code, expiresAt = pending.ExpiresAt });
    }

    private bool AllowedStoreUrl(Uri uri) => uri.Scheme == "https" ||
        (uri.Scheme == "http" && WooCommerceSecurity.LoopbackUrl(uri) &&
         WooCommerceSecurity.LocalDevelopment(HttpContext.RequestServices.GetRequiredService<IConfiguration>(), HttpContext.RequestServices.GetRequiredService<IHostEnvironment>()));

    [AllowAnonymous, HttpPost("authorize/poll/{id:guid}")]
    public async Task<IActionResult> Poll(Guid id)
    {
        Response.Headers.CacheControl = "no-store";
        var p = await db.Set<WooCommerceAuthorization>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.ExpiresAt > DateTime.UtcNow);
        var token = Request.Headers["X-OMS-Device-Secret"].ToString();
        if (p == null || token.Length != 64 || !WooCommerceSecurity.Matches(token, p.TokenHash)) return Unauthorized();
        if (!p.ConnectionId.HasValue) return Ok(new { state = "pending" });
        var c = await db.Set<WooCommerceConnection>().AsNoTracking().SingleAsync(x => x.Id == p.ConnectionId);
        if (!c.IsActive || c.AccessTokenHash != p.TokenHash) return Unauthorized();
        return Ok(new { state = "approved", connectionId = c.Id });
    }

    [Authorize, HttpGet("authorize/review")]
    public async Task<IActionResult> Review([FromQuery] string code)
    {
        await CheckManager();
        var hash = WooCommerceSecurity.Hash(code);
        var p = await db.Set<WooCommerceAuthorization>().AsNoTracking().SingleOrDefaultAsync(x => x.CodeHash == hash && x.ExpiresAt > DateTime.UtcNow && x.ConnectionId == null);
        return p == null ? NotFound() : Ok(new { p.StoreName, p.StoreUrl, p.ExpiresAt });
    }

    [Authorize, HttpPost("authorize/approve")]
    public async Task<IActionResult> Approve(WooApproveDto dto)
    {
        await CheckManager();
        var owner = Admin ? dto.OwnerUserId ?? 0 : UserId;
        if (!await db.Users.AnyAsync(x => x.Id == owner && x.RoleId == 4 && x.IsActive && !x.IsDeleted))
            throw new ValidationAppException("Select an active OMS customer account for this store.");
        await service.ValidateDefaults(dto.DefaultGenderId, dto.DefaultMaterialId, dto.DefaultStatusId);
        using var tx = await db.Database.BeginTransactionAsync();
        var hash = WooCommerceSecurity.Hash(dto.Code);
        await service.Lock($"woo-auth:{hash}");
        var p = await db.Set<WooCommerceAuthorization>().SingleOrDefaultAsync(x => x.CodeHash == hash && x.ExpiresAt > DateTime.UtcNow && x.ConnectionId == null)
            ?? throw new ValidationAppException("Authorization expired or already used. Start again from WordPress.");
        await service.Lock($"woo-install:{p.InstallationId}");
        var c = await db.Set<WooCommerceConnection>().SingleOrDefaultAsync(x => x.InstallationId == p.InstallationId);
        if (c != null)
        {
            await service.Lock($"woo:{c.Id}");
            if (c.Provider != "WooCommerce" || c.OwnerUserId != owner || c.StoreUrl != p.StoreUrl) throw new ConflictException("Installation already belongs to another owner or store.");
        }
        else
        {
            c = new WooCommerceConnection { InstallationId = p.InstallationId, OwnerUserId = owner, StoreUrl = p.StoreUrl };
            db.Add(c);
        }
        c.StoreName = p.StoreName;
        c.AccessTokenHash = p.TokenHash;
        c.PluginVersion = p.PluginVersion;
        c.IsActive = true;
        c.DefaultGenderId = dto.DefaultGenderId;
        c.DefaultMaterialId = dto.DefaultMaterialId;
        c.DefaultStatusId = dto.DefaultStatusId;
        await db.SaveChangesAsync();
        p.ConnectionId = c.Id;
        db.Add(new WooCommerceSyncLog { ConnectionId = c.Id, Result = "authorized", Message = "Store connection authorized; previous credential revoked." });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { connectionId = c.Id });
    }

    [Authorize, HttpGet("connections")]
    public async Task<IActionResult> Connections()
    {
        await CheckManager();
        return Ok(await db.Set<WooCommerceConnection>().AsNoTracking().Where(x => x.Provider == "WooCommerce" && (Admin || x.OwnerUserId == UserId))
            .Select(x => new { x.Id, x.OwnerUserId, x.StoreName, x.StoreUrl, x.IsActive, x.LastSyncAt, x.PluginVersion,
                x.DefaultGenderId, x.DefaultMaterialId, x.DefaultStatusId, x.StatusMappingsJson,
                orderCount = db.Set<WooCommerceOrder>().Count(o => o.ConnectionId == x.Id) }).ToListAsync());
    }

    [Authorize(Roles = "Admin,Super Admin"), HttpGet("owners")]
    public async Task<IActionResult> Owners()
    {
        await CheckManager();
        return Ok(await db.Users.AsNoTracking().Where(x => x.RoleId == 4 && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.FirstName).Select(x => new { x.Id, name = x.FirstName + " " + x.LastName, x.Email }).ToListAsync());
    }

    [Authorize, HttpPut("connections/{id:int}/settings")]
    public async Task<IActionResult> Settings(int id, WooSettingsDto dto)
    {
        var c = await Managed(id);
        await service.ValidateDefaults(dto.DefaultGenderId, dto.DefaultMaterialId, dto.DefaultStatusId);
        var allowed = new[] { "pending", "processing", "on-hold", "completed", "cancelled", "refunded", "failed" };
        if (dto.StatusMappings.Count > 100) return BadRequest();
        foreach (var pair in dto.StatusMappings)
            if (!allowed.Contains(pair.Value) || !await db.LookupItems.Where(OrderStatusCatalog.Selectable).AnyAsync(x => x.Id == pair.Key))
                throw new ValidationAppException("Invalid status mapping.");
        c.DefaultGenderId = dto.DefaultGenderId; c.DefaultMaterialId = dto.DefaultMaterialId; c.DefaultStatusId = dto.DefaultStatusId;
        c.StatusMappingsJson = JsonSerializer.Serialize(dto.StatusMappings);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize, HttpPost("connections/{id:int}/sync")]
    public async Task<IActionResult> Sync(int id)
    {
        var c = await Managed(id);
        if (!c.IsActive) throw new ConflictException("Reconnect the store first.");
        c.SyncRequestId = Guid.NewGuid();
        await db.SaveChangesAsync();
        return Accepted(new { message = "Full reconciliation requested. WordPress will collect this request on its next scheduled run." });
    }

    [Authorize, HttpPost("connections/{id:int}/disconnect")]
    public async Task<IActionResult> Disconnect(int id)
    {
        await Managed(id);
        await Revoke(id);
        return NoContent();
    }
    private async Task Revoke(int id)
    {
        using var tx = await db.Database.BeginTransactionAsync();
        await service.Lock($"woo:{id}");
        await db.Set<WooCommerceConnection>().Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false).SetProperty(x => x.AccessTokenHash, ""));
        db.Add(new WooCommerceSyncLog { ConnectionId = id, Result = "disconnected", Message = "Credential revoked. Imported orders retained." });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    [Authorize, HttpGet("connections/{id:int}/logs")]
    public async Task<IActionResult> Logs(int id, [FromQuery] long before = long.MaxValue)
    {
        await Managed(id);
        return Ok(await db.Set<WooCommerceSyncLog>().AsNoTracking().Where(x => x.ConnectionId == id && x.Id < before)
            .OrderByDescending(x => x.Id).Take(100).Select(x => new { x.Id, x.ExternalOrderId, x.CreatedDate, x.Result, x.Message }).ToListAsync());
    }

    [Authorize, HttpGet("orders/{orderId:int}")]
    public async Task<IActionResult> OrderSnapshot(int orderId)
    {
        if (!await db.Orders.Where(OrderVisibility.ForUser(UserId, User.IsInRole("Customer"))).AnyAsync(o => o.Id == orderId))
            return NotFound();
        var link = await db.Set<WooCommerceOrder>().AsNoTracking().Include(x => x.Buyer).Include(x => x.Connection)
            .SingleOrDefaultAsync(x => x.OrderId == orderId && !x.Order.IsDeleted);
        if (link == null) return NotFound();
        await Managed(link.ConnectionId);
        return Ok(new { link.ExternalOrderId, link.ExternalLineId, link.UnitNumber, link.IsCurrentUnit, link.ExternalStatus, link.Currency, link.Total, link.ModifiedAt, link.PaymentMethod,
            link.ItemsJson, link.BillingJson, link.ShippingJson, link.Connection.StoreName, link.Connection.StoreUrl,
            buyer = new { link.Buyer.Name, link.Buyer.Email, link.Buyer.Phone, link.Buyer.ExternalCustomerId } });
    }

    [Authorize(AuthenticationSchemes = WooCommerceSecurity.Scheme), HttpPost("orders")]
    public async Task<IActionResult> Import(WooOrderDto dto) => Ok(await service.Import(ConnectionId, dto));

    [Authorize(AuthenticationSchemes = WooCommerceSecurity.Scheme), HttpPost("disconnect")]
    public async Task<IActionResult> PluginDisconnect() { await Revoke(ConnectionId); return NoContent(); }

    [Authorize(AuthenticationSchemes = WooCommerceSecurity.Scheme), HttpGet("updates")]
    public async Task<IActionResult> Updates([FromQuery] int after = 0)
    {
        var c = await db.Set<WooCommerceConnection>().AsNoTracking().SingleAsync(x => x.Id == ConnectionId);
        var mappings = JsonSerializer.Deserialize<Dictionary<int, string>>(c.StatusMappingsJson)!;
        // Page whole WooCommerce orders, never individual units of the same order.
        var groups = await db.Set<WooCommerceOrder>().AsNoTracking().Where(x => x.ConnectionId == c.Id)
            .GroupBy(x => x.ExternalOrderId).Select(g => new { ExternalOrderId = g.Key, Id = g.Min(x => x.Id) })
            .Where(x => x.Id > after).OrderBy(x => x.Id).Take(100).ToListAsync();
        var externalIds = groups.Select(x => x.ExternalOrderId).ToArray();
        var orders = await db.Set<WooCommerceOrder>().AsNoTracking().Where(x => x.ConnectionId == c.Id && externalIds.Contains(x.ExternalOrderId))
            .OrderBy(x => x.Id).Select(x => new { x.ExternalOrderId, x.IsCurrentUnit, x.Order.OrderStatusId, x.Order.TrackingNumber, x.Order.IsDeleted }).ToListAsync();
        return Ok(new {
            c.SyncRequestId,
            nextCursor = groups.Count == 100 ? groups[^1].Id : 0,
            items = groups.Select(g => {
                var units = orders.Where(x => x.ExternalOrderId == g.ExternalOrderId && x.IsCurrentUnit && !x.IsDeleted).ToArray();
                var statuses = units.Select(x => mappings.GetValueOrDefault(x.OrderStatusId)).Distinct().ToArray();
                var status = statuses.Length == 1 ? statuses[0] : null;
                var tracking = string.Join(", ", units.Select(x => x.TrackingNumber).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
                return new { g.ExternalOrderId, deleted = units.Length == 0, status, TrackingNumber = tracking,
                    revision = WooCommerceSecurity.Hash($"{status}|{tracking}|{units.Length == 0}") };
            })
        });
    }
}
