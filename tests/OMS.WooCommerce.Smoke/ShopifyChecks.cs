using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using OMS_Backend.Controllers;
using OMS_Backend.Data;
using OMS_Backend.Models;
using OMS_Backend.Services;

internal static class ShopifyChecks {
    public static async Task Run(OMSDbContext db,int owner,int status,Action<bool,string> check) {
        const string secret="fixture-secret";
        var body=Encoding.UTF8.GetBytes("{\"id\":20}");
        var signature=Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),body));
        check(ShopifySecurity.VerifyBody(body,signature,secret),"Shopify verifies raw webhook HMAC");
        check(!ShopifySecurity.VerifyBody(Encoding.UTF8.GetBytes("{}"),signature,secret)&&!ShopifySecurity.VerifyBody(body,"invalid",secret),"Shopify rejects tampered and malformed webhooks");
        check(ShopifySecurity.ValidShop("my-store.myshopify.com")&&!ShopifySecurity.ValidShop("myshopify.com.evil.test")&&!ShopifySecurity.ValidShop("localhost")&&!ShopifySecurity.ValidShop("a.myshopify.com/path"),"Shopify domain validation prevents arbitrary upstream URLs");
        var query=new Dictionary<string,StringValues>{{"shop","my-store.myshopify.com"},{"state","nonce"},{"code","fixture"}};
        query["hmac"]=Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),Encoding.UTF8.GetBytes("code=fixture&shop=my-store.myshopify.com&state=nonce")));
        check(ShopifySecurity.VerifyQuery(new QueryCollection(query),secret),"Shopify OAuth query signature validates");
        query["state"]=new StringValues(new[]{"nonce","other"});
        check(!ShopifySecurity.VerifyQuery(new QueryCollection(query),secret),"Shopify rejects duplicate OAuth parameters");
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Shopify:ClientSecret",secret},{"Shopify:Enabled","true"}}).Build();
        var factory=new FakeShopify();var api=new ShopifyApi(factory,new EphemeralDataProtectionProvider(),config,db);
        var store=new ShopifyStore{Shop="my-store.myshopify.com",Connection=new(){Provider="Shopify",OwnerUserId=owner,InstallationId=Guid.NewGuid(),StoreUrl="https://my-store.myshopify.com",StoreName="Shopify test",IsActive=true,DefaultGenderId=5,DefaultMaterialId=12,DefaultStatusId=status}};
        db.Add(store);await db.SaveChangesAsync();
        await api.SetToken(store,JsonSerializer.SerializeToElement(new{access_token="test-token",scope="read_orders,read_products"}));
        check(store.ProtectedToken!="test-token","Shopify token encrypted at rest");
        var sync=new ShopifySyncService(db,api,new(db));
        factory.Reply=OrderResponse(2);await sync.Import(store,20);await sync.Import(store,20);
        var links=await db.Set<WooCommerceOrder>().Include(x=>x.Order).Where(x=>x.ConnectionId==store.ConnectionId).ToListAsync();
        check(links.Count==2&&links.All(x=>x.Order.ManufacturerOrderNumber.StartsWith("SH")),"Shopify quantity two imports two SH rows and repeated delivery is idempotent");
        check(JsonDocument.Parse(links[0].ItemsJson).RootElement[0].GetProperty("ImageUrls").GetArrayLength()==2,"Shopify featured and gallery images imported without duplicates");
        links[0].Order.CustomerProductTitle="Production edit";await db.SaveChangesAsync();
        factory.Reply=OrderResponse(3);await sync.Import(store,20);
        check(await db.Set<WooCommerceOrder>().CountAsync(x=>x.ConnectionId==store.ConnectionId)==3&&links[0].Order.CustomerProductTitle=="Production edit","Shopify quantity increase preserves edited production row");
        var reader=new OrderService(db,null!,config);var detail=await reader.GetOrderByIdAsync(links[0].OrderId,owner,true);
        check(detail.Source=="Shopify"&&detail.Images.Count==2,"Shopify source and image gallery exposed to OMS");
        var controller=new ShopifyIntegrationController(db,api,config,new(db)){ControllerContext=new(){HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("userId",owner.ToString()),new Claim(ClaimTypes.Role,"Customer")},"test"))}}};
        async Task<IActionResult> Webhook(string eventId,string hmac){controller.Request.Body=new MemoryStream(body);controller.Request.Headers["X-Shopify-Hmac-Sha256"]=hmac;controller.Request.Headers["X-Shopify-Shop-Domain"]=store.Shop;controller.Request.Headers["X-Shopify-Topic"]="orders/updated";controller.Request.Headers["X-Shopify-Event-Id"]=eventId;return await controller.Webhook();}
        check(await Webhook("unique-event",signature) is OkResult&&await Webhook("unique-event",signature) is OkResult&&await db.Set<ShopifyJob>().CountAsync(x=>x.StoreId==store.Id)==1,"Shopify webhook queue deduplicates repeated event IDs");
        check(await Webhook("bad-event","invalid") is UnauthorizedResult,"Shopify rejects unsigned webhook before enqueueing");
        var redaction=new ShopifyJob{Store=store,Topic="customers/redact",EventId="redaction",ExternalId=87};db.Add(redaction);await db.SaveChangesAsync();await sync.Process(redaction);
        factory.Reply=OrderResponse(3);await sync.Import(store,20);
        check((await db.Set<WooCommerceCustomer>().SingleAsync(x=>x.ConnectionId==store.ConnectionId)).Email==""&&links[0].Order.ConsigneeName=="Redacted","Shopify redaction survives later order reconciliation");
        store.FulfillmentEnabled=true;store.ShippedStatusId=status;
        factory.Reply=FulfillmentResponse(4);
        var rejected=false;try{await sync.Fulfill(store);}catch(InvalidOperationException ex){rejected=ex.Message.Contains("quantities changed");}
        check(rejected&&factory.Mutations==0,"Shopify refuses fulfillment when remote quantity exceeds reviewed OMS units");
        factory.Reply=FulfillmentResponse(3);await sync.Fulfill(store);var calls=factory.Mutations;await sync.Fulfill(store);
        check(calls==2&&factory.Mutations==calls,"Shopify fulfillment receipts prevent repeated creation and tracking mutations");
        links[0].Order.TrackingNumber="NEW-TRACKING";await db.SaveChangesAsync();
        factory.Reply=FulfillmentResponse(3).Replace("CREATE_FULFILLMENT","HOLD");var held=false;
        try{await sync.Fulfill(store);}catch(InvalidOperationException ex){held=ex.Message.Contains("not ready");}
        check(held&&factory.Mutations==calls,"Shopify held fulfillment remains retryable without sending a mutation");
        factory.Reply=OrderResponse(0);await sync.Import(store,20);
        check(await db.Set<WooCommerceOrder>().CountAsync(x=>x.ConnectionId==store.ConnectionId)==3&&!await db.Set<WooCommerceOrder>().AnyAsync(x=>x.ConnectionId==store.ConnectionId&&x.IsCurrentUnit),"Shopify removal of all units retains production rows for review");
        await controller.Disconnect(store.Id);
        check(!store.Connection.IsActive&&store.ProtectedToken==""&&await db.Set<WooCommerceOrder>().CountAsync(x=>x.ConnectionId==store.ConnectionId)==3,"Shopify disconnect clears token and preserves production rows");
    }
    private static string OrderResponse(int qty)=>JsonSerializer.Serialize(new{data=new{order=new{
        id="gid://shopify/Order/20",name="#1020",email="buyer@example.test",note="fixture",updatedAt="2026-09-18T01:00:00Z",cancelledAt=(string?)null,displayFulfillmentStatus="UNFULFILLED",paymentGatewayNames=new[]{"test"},customer=new{id="gid://shopify/Customer/87"},
        currentTotalPriceSet=new{shopMoney=new{amount="100.50",currencyCode="USD"}},billingAddress=new{firstName="Buyer",address1="Fixture address"},shippingAddress=(object?)null,
        lineItems=new{pageInfo=new{hasNextPage=false,endCursor=(string?)null},nodes=new[]{new{id="gid://shopify/LineItem/3",name="Shirt",sku="TEST",currentQuantity=qty,customAttributes=new[]{new{key="Size",value="M"}},discountedTotalSet=new{shopMoney=new{amount="100.50"}},image=new{url="https://cdn.shopify.com/featured.jpg"},variant=(object?)null,product=new{id="gid://shopify/Product/4",images=new{nodes=new[]{new{url="https://cdn.shopify.com/featured.jpg"},new{url="https://cdn.shopify.com/back.jpg"}}}}}}}
    }}});
    private static string FulfillmentResponse(int qty)=>JsonSerializer.Serialize(new{data=new{order=new{cancelledAt=(string?)null,fulfillmentOrders=new{pageInfo=new{hasNextPage=false},nodes=new[]{new{id="gid://shopify/FulfillmentOrder/1",status="OPEN",supportedActions=new[]{new{action="CREATE_FULFILLMENT"}},lineItems=new{pageInfo=new{hasNextPage=false},nodes=new[]{new{id="gid://shopify/FulfillmentOrderLineItem/1",remainingQuantity=qty,lineItem=new{id="gid://shopify/LineItem/3"}}}}}}}}}});
    private sealed class FakeShopify:HttpMessageHandler,IHttpClientFactory {
        public string Reply="{}";public int Mutations;
        public HttpClient CreateClient(string name)=>new(this,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
            var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));var query=json.RootElement.GetProperty("query").GetString()!;
            var response=Reply;
            if(query.Contains("{product(id:"))response="{\"data\":{\"product\":{\"images\":{\"nodes\":[{\"url\":\"https://cdn.shopify.com/featured.jpg\"},{\"url\":\"https://cdn.shopify.com/back.jpg\"}]}}}}";
            if(query.StartsWith("mutation")){Mutations++;var name=query.Contains("fulfillmentCreate(")?"fulfillmentCreate":"fulfillmentTrackingInfoUpdate";response=JsonSerializer.Serialize(new{data=new Dictionary<string,object>{{name,new{fulfillment=new{id="gid://shopify/Fulfillment/1"},userErrors=Array.Empty<object>()}}}});}
            return new(HttpStatusCode.OK){Content=new StringContent(response,Encoding.UTF8,"application/json")};
        }
    }
}
