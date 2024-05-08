namespace eShop;

using Microsoft.eShopOnContainers.WebMVC.ViewModels;
using Newtonsoft.Json;

class ApplyCouponRequest {
    [JsonProperty("coupon_code")]
    public string CouponCode { get; set; }
    [JsonProperty("items")]
    public Item[] Items { get; set; }
}

class ApplyCouponResponse {
    [JsonProperty("adjusted_items")]
    public AdjustedItem[] Items { get; set; }
    [JsonProperty("final_price")]
    public float FinalPrice { get; set; }
}

class AdjustedItem {
    [JsonProperty("id")]
    public string ProductId { get; set; }
    [JsonProperty("original_unit_price")]
    public float OriginalPrice { get; set; }
    [JsonProperty("adjusted_unit_price")]
    public float AdjustedPrice { get; set; }
    [JsonProperty("original_units")]
    public int OriginalUnits { get; set; }
    [JsonProperty("adjusted_units")]
    public int AdjustedUnits { get; set; }
    [JsonProperty("name")]
    public string Name { get; set; }
}

class Item {
    [JsonProperty("id")]
    public string ProductId { get; set; }
    [JsonProperty("name")]
    public string ProductName { get; set; }
    [JsonProperty("unit_price")]
    public float UnitPrice { get; set; }
    [JsonProperty("units")]
    public int Units { get; set; }
}

[Authorize]
public class OrderController : Controller
{
    private IOrderingService _orderSvc;
    private IBasketService _basketSvc;
    private readonly IIdentityParser<ApplicationUser> _appUserParser;
    private readonly HttpClient _httpClient = new HttpClient();
    public OrderController(IOrderingService orderSvc, IBasketService basketSvc, IIdentityParser<ApplicationUser> appUserParser)
    {
        _appUserParser = appUserParser;
        _orderSvc = orderSvc;
        _basketSvc = basketSvc;
    }

    public async Task<IActionResult> Create()
    {

        // var user = _appUserParser.Parse(HttpContext.User);
        // var order = await _basketSvc.GetOrderDraft(user.Id);
        // var vm = _orderSvc.MapUserInfoIntoOrder(user, order);
        // var jsonString = JsonConvert.SerializeObject(
        //    vm, Formatting.Indented);
        var jsonString = @"{
   ""OrderNumber"": null,
   ""Date"": ""0001-01-01T00:00:00"",
   ""Status"": null,
   ""Total"": 40.0,
   ""Description"": null,
   ""City"": ""Redmond"",
   ""Street"": ""15703 NE 61st Ct"",
   ""State"": ""WA"",
   ""Country"": ""U.S."",
   ""ZipCode"": ""98052"",
   ""CardNumber"": ""4012888888881881"",
   ""CardHolderName"": ""Alice Smith"",
   ""CardExpiration"": ""2024-12-01T00:00:00"",
   ""CardExpirationShort"": null,
   ""CardSecurityNumber"": ""123"",
   ""CardTypeId"": 0,
   ""Buyer"": ""0132ee44-345d-4abd-badc-658d2a5d0597"",
   ""ActionCodeSelectList"": [],
   ""OrderItems"": [
     {
       ""ProductId"": 6,
       ""ProductName"": "".NET Blue Hoodie"",
       ""UnitPrice"": 12.0,
       ""Discount"": 0.0,
       ""Units"": 1,
       ""PictureUrl"": ""https://images.footballfanatics.com/brooklyn-nets/brooklyn-nets-nike-city-edition-courtside-fleece-hoodie-royal-blue-mens_ss4_p-13315744+u-p3q3oj093zgie7h07j1h+v-3b5d7df7f82d44deb9ed2ed88f4c708c.jpg?_hv=2&w=900""
     },
     {
       ""ProductId"": 1,
       ""ProductName"": "".NET Bot Black Hoodie"",
       ""UnitPrice"": 19.5,
       ""Discount"": 0.0,
       ""Units"": 1,
       ""PictureUrl"": ""https://images.footballfanatics.com/brooklyn-nets/mens-fanatics-branded-black-brooklyn-nets-wordmark-pullover-hoodie_pi2885000_altimages_ff_2885069alt1_full.jpg?_hv=2&w=900""
     },
     {
       ""ProductId"": 2,
       ""ProductName"": "".NET Black & White Mug"",
       ""UnitPrice"": 8.5,
       ""Discount"": 0.0,
       ""Units"": 1,
       ""PictureUrl"": ""https://assets.weimgs.com/weimgs/rk/images/wcm/products/202403/0004/img53o.jpg""
     }
   ],
   ""RequestId"": ""00000000-0000-0000-0000-000000000000""
 }";

        Console.WriteLine("View: Create");
        Console.WriteLine(jsonString);

        var deserialized = JsonConvert.DeserializeObject<Order>(jsonString);
        deserialized.CardExpirationShortFormat();

        return View(deserialized);
    }

    [HttpPost]
    public async Task<IActionResult> Checkout(Order model)
    {
        try
        {
            if (ModelState.IsValid)
            {
                var user = _appUserParser.Parse(HttpContext.User);
                var basket = _orderSvc.MapOrderToBasket(model);

                await _basketSvc.Checkout(basket);

                //Redirect to historic list.
                return RedirectToAction("Index");
            }
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("Error", $"It was not possible to create a new order, please try later on ({ex.GetType().Name} - {ex.Message})");
        }

        
        return View("Create", model);
    }

    [HttpPost]
    public async Task<IActionResult> ApplyCouponAsync(string couponCode, string orderModelJson)
    {
        if (string.IsNullOrWhiteSpace(orderModelJson))
        {
            return BadRequest("Invalid order model data.");
        }

        var orderModel = JsonConvert.DeserializeObject<Order>(orderModelJson);
        if (orderModel == null)
        {
            return BadRequest("Order deserialization failed.");
        }

        var request = new ApplyCouponRequest
        {
            CouponCode = couponCode,
            Items = orderModel.OrderItems.Select(item => new Item
            {
                ProductId = item.ProductId.ToString(),
                ProductName = item.ProductName,
                UnitPrice = (float)item.UnitPrice,
                Units = item.Units
            }).ToArray()
        };

        var serializedJson = JsonConvert.SerializeObject(request);
        var content = new StringContent(serializedJson, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.PostAsync("http://coupon-django-api:8000/coupons/apply", content);
            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ApplyCouponResponse>(responseBody);

                var idToPictures = orderModel.OrderItems.ToDictionary(item => item.ProductId.ToString(), item => item.PictureUrl);

                orderModel.Total = (decimal)result.FinalPrice;
                orderModel.OrderItems = result.Items.Select(item => new OrderItem
                {
                    ProductId = int.Parse(item.ProductId),
                    ProductName = item.Name,
                    UnitPrice = (decimal)item.AdjustedPrice,
                    Units = item.AdjustedUnits,
                    Discount = (decimal)(item.OriginalPrice * item.OriginalUnits - item.AdjustedPrice * item.AdjustedUnits),
                    PictureUrl = idToPictures[item.ProductId],
                }).ToList();

                return PartialView("_OrderItems", orderModel);
            }
            else {
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
            }
        }
        catch (Exception)
        {
            return StatusCode(500, "Internal server error");
        }
    }

    
    public async Task<IActionResult> Cancel(string orderId)
    {
        await _orderSvc.CancelOrder(orderId);

        //Redirect to historic list.
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Detail(string orderId)
    {
        var user = _appUserParser.Parse(HttpContext.User);

        var order = await _orderSvc.GetOrder(user, orderId);
        return View(order);
    }

    public async Task<IActionResult> Index(Order item)
    {
        var user = _appUserParser.Parse(HttpContext.User);
        var vm = await _orderSvc.GetMyOrders(user);
        return View(vm);
    }
}
