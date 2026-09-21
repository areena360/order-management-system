using System.Globalization;
using System.Text.Json;
using OMS_Backend.DTOs;
namespace OMS_Backend.Services;
public static class ShopifyOrderMapper {
    public static string Text(JsonElement e,string name) => e.ValueKind==JsonValueKind.Object && e.TryGetProperty(name,out var v) && v.ValueKind!=JsonValueKind.Null ? v.ToString() : "";
    public static long Id(string gid) => long.TryParse(gid.Split('/').Last(),out var id)?id:0;
    public static WooAddressDto Address(JsonElement e,string email="") => new() {
        FirstName=Text(e,"firstName"),LastName=Text(e,"lastName"),Company=Text(e,"company"),
        Address1=Text(e,"address1"),Address2=Text(e,"address2"),City=Text(e,"city"),State=Text(e,"province"),Postcode=Text(e,"zip"),Country=Text(e,"countryCodeV2"),Phone=Text(e,"phone"),Email=email
    };
    public static WooOrderDto Map(JsonElement order,List<JsonElement> lines) {
        var money=order.GetProperty("currentTotalPriceSet").GetProperty("shopMoney");
        var dto=new WooOrderDto {
            Id=Id(Text(order,"id")),Number=Text(order,"name"),Status=Text(order,"cancelledAt")!=""?"cancelled":Text(order,"displayFulfillmentStatus").ToLowerInvariant(),
            Currency=Text(money,"currencyCode"),Total=decimal.Parse(Text(money,"amount"),CultureInfo.InvariantCulture),
            ModifiedAt=DateTime.Parse(Text(order,"updatedAt"),CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal),
            CustomerNote=Text(order,"note"),PaymentMethod=string.Join(", ",order.GetProperty("paymentGatewayNames").EnumerateArray().Select(x=>x.GetString())),
            Billing=Address(order.GetProperty("billingAddress"),Text(order,"email")),Shipping=Address(order.GetProperty("shippingAddress")),
            CustomerId=order.TryGetProperty("customer",out var customer)?Id(Text(customer,"id")):0
        };
        foreach(var line in lines) {
            var quantity=line.GetProperty("currentQuantity").GetInt32(); if(quantity<=0) continue;
            var images=new List<string>();
            if(line.TryGetProperty("image",out var image)&&Text(image,"url")!="") images.Add(Text(image,"url"));
            if(line.TryGetProperty("product",out var product)&&product.ValueKind==JsonValueKind.Object&&product.TryGetProperty("images",out _))
                foreach(var edge in product.GetProperty("images").GetProperty("nodes").EnumerateArray()) images.Add(Text(edge,"url"));
            var attributes=line.GetProperty("customAttributes").EnumerateArray().Select(x=>$"{Text(x,"key")}: {Text(x,"value")}").ToList();
            if(line.TryGetProperty("variant",out var variant)&&variant.ValueKind==JsonValueKind.Object)
                attributes.AddRange(variant.GetProperty("selectedOptions").EnumerateArray().Select(x=>$"{Text(x,"name")}: {Text(x,"value")}"));
            dto.Items.Add(new WooLineDto { Id=Id(Text(line,"id")),ProductId=Id(Text(product,"id")),VariationId=Id(Text(variant,"id")),Name=Text(line,"name"),Sku=Text(line,"sku"),Quantity=quantity,
                Total=decimal.Parse(Text(line.GetProperty("discountedTotalSet").GetProperty("shopMoney"),"amount"),CultureInfo.InvariantCulture),
                Attributes=string.Join("; ",attributes),ImageUrl=images.FirstOrDefault()??"",ImageUrls=images.Distinct().ToList() });
        }
        return dto;
    }
}
