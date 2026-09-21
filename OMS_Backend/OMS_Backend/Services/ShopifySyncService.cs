using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;
using OMS_Backend.Models;
namespace OMS_Backend.Services;

public class ShopifySyncService(OMSDbContext db,ShopifyApi api,WooCommerceIntegrationService importer) {
    private const string OrderQuery="""
      query($id:ID!,$after:String){order(id:$id){id name email note updatedAt cancelledAt displayFulfillmentStatus paymentGatewayNames customer{id}
      currentTotalPriceSet{shopMoney{amount currencyCode}}
      billingAddress{firstName lastName company address1 address2 city province zip countryCodeV2 phone}
      shippingAddress{firstName lastName company address1 address2 city province zip countryCodeV2 phone}
      lineItems(first:100,after:$after){pageInfo{hasNextPage endCursor} nodes{id name sku currentQuantity customAttributes{key value}
      discountedTotalSet{shopMoney{amount}} image{url} variant{id selectedOptions{name value}} product{id}}}}}
      """;
    public async Task Import(ShopifyStore store,long orderId) {
        var lines=new List<JsonElement>();JsonElement order=default;string? cursor=null;
        do {
            var data=await api.Query(store,OrderQuery,new{id=$"gid://shopify/Order/{orderId}",after=cursor});order=data.GetProperty("order");
            if(order.ValueKind==JsonValueKind.Null)throw new InvalidOperationException("Order unavailable; verify Shopify order access and retention window.");
            var page=order.GetProperty("lineItems");lines.AddRange(page.GetProperty("nodes").EnumerateArray().Select(x=>x.Clone()));
            cursor=page.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean()?page.GetProperty("pageInfo").GetProperty("endCursor").GetString():null;
            if(lines.Count>200)throw new InvalidOperationException("Order exceeds the supported 200 product lines.");
        } while(cursor!=null);
        var dto=ShopifyOrderMapper.Map(order,lines);
        // Fetch each product once, separately: nesting 100 images under 100 lines
        // would exceed Shopify's requested GraphQL query-cost limit.
        foreach(var product in dto.Items.Where(x=>x.ProductId>0).GroupBy(x=>x.ProductId)) {
            var data=await api.Query(store,"query($id:ID!){product(id:$id){images(first:100){nodes{url}}}}",new{id=$"gid://shopify/Product/{product.Key}"});
            var value=data.GetProperty("product");if(value.ValueKind==JsonValueKind.Null)continue;
            var images=value.GetProperty("images").GetProperty("nodes").EnumerateArray().Select(x=>ShopifyOrderMapper.Text(x,"url")).ToArray();
            foreach(var line in product)line.ImageUrls=line.ImageUrls.Concat(images).Distinct().Take(100).ToList();
        }
        if(dto.CustomerId>0&&await db.Set<ShopifyJob>().AnyAsync(x=>x.StoreId==store.Id&&x.Topic=="customers/redact"&&x.ExternalId==dto.CustomerId)) {
            dto.Billing=new(){FirstName="Redacted"};dto.Shipping=new();dto.CustomerNote="";
            foreach(var line in dto.Items)line.Attributes="";
        }
        if(dto.Items.Count==0) {
            using var tx=await db.Database.BeginTransactionAsync();await importer.Lock($"woo:{store.ConnectionId}");
            foreach(var link in await db.Set<WooCommerceOrder>().Where(x=>x.ConnectionId==store.ConnectionId&&x.ExternalOrderId==orderId).ToListAsync()) {
                if(link.ModifiedAt>dto.ModifiedAt)continue;
                link.IsCurrentUnit=false;link.ExternalStatus=dto.Status;link.ModifiedAt=dto.ModifiedAt;link.Total=dto.Total;
            }
            store.Connection.LastSyncAt=DateTime.UtcNow;
            await db.SaveChangesAsync();await tx.CommitAsync();return;
        }
        await importer.Import(store.ConnectionId,dto);
    }
    public async Task Scan(ShopifyStore store) {
        store.ScanUntil??=DateTime.UtcNow;
        var search=$"updated_at:>={store.ScanSince:yyyy-MM-ddTHH:mm:ssZ} updated_at:<={store.ScanUntil:yyyy-MM-ddTHH:mm:ssZ}";
        var data=await api.Query(store,"query($after:String,$q:String!){orders(first:50,after:$after,sortKey:UPDATED_AT,query:$q){nodes{id} pageInfo{hasNextPage endCursor}}}",new{after=store.ScanCursor,q=search});
        var page=data.GetProperty("orders");
        foreach(var order in page.GetProperty("nodes").EnumerateArray()) {
            var id=ShopifyOrderMapper.Id(ShopifyOrderMapper.Text(order,"id"));
            var eventId=$"scan:{store.ScanUntil:O}:{id}";
            if(!await db.Set<ShopifyJob>().AnyAsync(x=>x.StoreId==store.Id&&x.EventId==eventId)) db.Add(new ShopifyJob{StoreId=store.Id,EventId=eventId,ExternalId=id});
        }
        if(page.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean()) store.ScanCursor=page.GetProperty("pageInfo").GetProperty("endCursor").GetString();
        else {store.ScanSince=store.ScanUntil.Value.AddMinutes(-2);store.ScanUntil=null;store.ScanCursor=null;store.NextPollAt=DateTime.UtcNow.AddMinutes(5);}
        await db.SaveChangesAsync();
    }
    public async Task Process(ShopifyJob job) {
        var store=job.Store;
        if(job.Topic=="shop/redact"||job.Topic=="customers/redact") {await Redact(store,job.Topic=="shop/redact"?null:job.ExternalId);return;}
        if(job.Topic=="customers/data_request") {throw new InvalidOperationException("Shopify privacy request: export customer data in store logs, deliver through a verified channel, then mark resolved.");}
        if(!store.Connection.IsActive) {job.Error="Connection inactive; job skipped.";return;}
        if(job.Topic=="products/update") {
            // Refresh only already imported orders; never import additional history for a product edit.
            var snapshots=await db.Set<WooCommerceOrder>().Where(x=>x.ConnectionId==store.ConnectionId).Select(x=>new{x.ExternalOrderId,x.ItemsJson}).ToListAsync();
            foreach(var id in snapshots.Where(x=>(JsonSerializer.Deserialize<List<OMS_Backend.DTOs.WooLineDto>>(x.ItemsJson)??new()).Any(i=>i.ProductId==job.ExternalId)).Select(x=>x.ExternalOrderId).Distinct()) {
                var key=$"product:{job.Id}:{id}";if(!await db.Set<ShopifyJob>().AnyAsync(x=>x.StoreId==store.Id&&x.EventId==key))db.Add(new ShopifyJob{StoreId=store.Id,EventId=key,ExternalId=id});
            }
            await db.SaveChangesAsync();return;
        }
        await Import(store,job.ExternalId);
    }
    private async Task Redact(ShopifyStore store,long? customerId) {
        var links=await db.Set<WooCommerceOrder>().Include(x=>x.Buyer).Include(x=>x.Order).Where(x=>x.ConnectionId==store.ConnectionId&&(!customerId.HasValue||x.Buyer.ExternalCustomerId==customerId)).ToListAsync();
        foreach(var link in links) {
            link.Buyer.Name="Redacted";link.Buyer.Email="";link.Buyer.Phone="";
            var items=JsonSerializer.Deserialize<List<OMS_Backend.DTOs.WooLineDto>>(link.ItemsJson)??new();foreach(var item in items)item.Attributes="";link.ItemsJson=JsonSerializer.Serialize(items);
            link.BillingJson="{}";link.ShippingJson="{}";link.Order.ConsigneeName="Redacted";link.Order.ConsigneeAddress="";link.Order.NotesByCustomer="";
        }
        if(!customerId.HasValue){store.Connection.IsActive=false;store.ProtectedToken="";store.ProtectedRefreshToken="";}
        await db.SaveChangesAsync();
    }
    public async Task Fulfill(ShopifyStore store) {
        if(!store.FulfillmentEnabled||!store.ShippedStatusId.HasValue)return;
        var units=await db.Set<WooCommerceOrder>().Include(x=>x.Order).Where(x=>x.ConnectionId==store.ConnectionId&&x.IsCurrentUnit&&!x.Order.IsDeleted).ToListAsync();
        foreach(var group in units.GroupBy(x=>x.ExternalOrderId).Where(g=>g.All(x=>x.Order.OrderStatusId==store.ShippedStatusId))) {
            var numbers=group.Select(x=>x.Order.TrackingNumber).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
            var hash=WooCommerceSecurity.Hash(JsonSerializer.Serialize(new {numbers, units=group.OrderBy(x=>x.ExternalLineId).ThenBy(x=>x.UnitNumber).Select(x=>new{x.ExternalLineId,x.UnitNumber})}));
            var saved=await db.Set<ShopifyShipment>().SingleOrDefaultAsync(x=>x.StoreId==store.Id&&x.ExternalOrderId==group.Key);
            if(saved!=null&&saved.TrackingHash==hash)continue;
            var data=await api.Query(store,"query($id:ID!){order(id:$id){cancelledAt fulfillmentOrders(first:10){pageInfo{hasNextPage} nodes{id status requestStatus supportedActions{action} lineItems(first:50){pageInfo{hasNextPage} nodes{id remainingQuantity lineItem{id}}}}}}}",new{id=$"gid://shopify/Order/{group.Key}"});
            var order=data.GetProperty("order");if(order.ValueKind==JsonValueKind.Null||ShopifyOrderMapper.Text(order,"cancelledAt")!="")continue;
            var connection=order.GetProperty("fulfillmentOrders");
            if(connection.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean())throw new InvalidOperationException("Shopify fulfillment order count exceeds 10; fulfill this order manually.");
            var fulfillmentIds=saved==null?new List<string>():JsonSerializer.Deserialize<List<string>>(saved.FulfillmentIdsJson)!;
            if(connection.GetProperty("nodes").EnumerateArray().Any(x=>x.GetProperty("lineItems").GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean()))
                throw new InvalidOperationException("Shopify fulfillment has over 50 lines; fulfill manually.");
            var remoteLines=connection.GetProperty("nodes").EnumerateArray().SelectMany(x=>x.GetProperty("lineItems").GetProperty("nodes").EnumerateArray()).ToList();
            foreach(var line in remoteLines.GroupBy(x=>ShopifyOrderMapper.Id(ShopifyOrderMapper.Text(x.GetProperty("lineItem"),"id"))))
                if(line.Sum(x=>x.GetProperty("remainingQuantity").GetInt32())>group.Count(x=>x.ExternalLineId==line.Key))
                    throw new InvalidOperationException("Shopify quantities changed; sync and review the order before fulfillment.");
            if(saved==null){saved=new ShopifyShipment{StoreId=store.Id,ExternalOrderId=group.Key};db.Add(saved);await db.SaveChangesAsync();}
            foreach(var fo in connection.GetProperty("nodes").EnumerateArray()) {
                var remaining=fo.GetProperty("lineItems").GetProperty("nodes").EnumerateArray().Any(x=>x.GetProperty("remainingQuantity").GetInt32()>0);
                if(!remaining)continue;
                if(!fo.GetProperty("supportedActions").EnumerateArray().Any(x=>ShopifyOrderMapper.Text(x,"action")=="CREATE_FULFILLMENT") ||
                    (ShopifyOrderMapper.Text(fo,"status")!="OPEN" && ShopifyOrderMapper.Text(fo,"status")!="IN_PROGRESS"))
                    throw new InvalidOperationException("Shopify fulfillment is held or not ready; automatic retry scheduled.");
                var items=fo.GetProperty("lineItems");if(items.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean())throw new InvalidOperationException("Shopify fulfillment has over 50 lines; fulfill manually.");
                var mapped=items.GetProperty("nodes").EnumerateArray().Where(x=>x.GetProperty("remainingQuantity").GetInt32()>0&&group.Any(u=>u.ExternalLineId==ShopifyOrderMapper.Id(ShopifyOrderMapper.Text(x.GetProperty("lineItem"),"id")))).Select(x=>new{id=ShopifyOrderMapper.Text(x,"id"),quantity=x.GetProperty("remainingQuantity").GetInt32()}).ToArray();
                if(mapped.Length==0)continue;
                var result=await api.Query(store,"mutation($f:FulfillmentInput!){fulfillmentCreate(fulfillment:$f){fulfillment{id} userErrors{field message}}}",new{f=new{notifyCustomer=false,trackingInfo=new{numbers},lineItemsByFulfillmentOrder=new[]{new{fulfillmentOrderId=ShopifyOrderMapper.Text(fo,"id"),fulfillmentOrderLineItems=mapped}}}});
                var mutation=result.GetProperty("fulfillmentCreate");ShopifyApi.CheckMutation(mutation);fulfillmentIds.Add(ShopifyOrderMapper.Text(mutation.GetProperty("fulfillment"),"id")); saved.FulfillmentIdsJson=JsonSerializer.Serialize(fulfillmentIds.Distinct());await db.SaveChangesAsync();
            }
            if(saved!=null)foreach(var id in JsonSerializer.Deserialize<List<string>>(saved.FulfillmentIdsJson)!) {
                var result=await api.Query(store,"mutation($id:ID!,$t:FulfillmentTrackingInput!){fulfillmentTrackingInfoUpdate(fulfillmentId:$id,trackingInfoInput:$t,notifyCustomer:false){fulfillment{id} userErrors{field message}}}",new{id,t=new{numbers}});ShopifyApi.CheckMutation(result.GetProperty("fulfillmentTrackingInfoUpdate"));
            }
            if(saved==null){saved=new ShopifyShipment{StoreId=store.Id,ExternalOrderId=group.Key};db.Add(saved);}
            saved.FulfillmentIdsJson=JsonSerializer.Serialize(fulfillmentIds.Distinct());saved.TrackingHash=hash;await db.SaveChangesAsync();
        }
    }
}


