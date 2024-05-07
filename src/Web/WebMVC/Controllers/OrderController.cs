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

        var user = _appUserParser.Parse(HttpContext.User);
        var order = await _basketSvc.GetOrderDraft(user.Id);
        var vm = _orderSvc.MapUserInfoIntoOrder(user, order);
        vm.CardExpirationShortFormat();

        return View(vm);
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
