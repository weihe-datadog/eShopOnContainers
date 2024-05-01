namespace Microsoft.eShopOnContainers.WebMVC.Controllers;

using Microsoft.eShopOnContainers.WebMVC.ViewModels;
using Newtonsoft.Json;

class ApplyCouponRequest {
    [JsonProperty("coupon_code")]
    public string CouponCode { get; set; }
    [JsonProperty("items")]
    public Item[] items { get; set; }
}

class ApplyCouponResponse {
    [JsonProperty("adjusted_item_prices")]
    public AdjustedItemPrices[] items { get; set; }
    [JsonProperty("final_price")]
    public float FinalPrice { get; set; }
}

class AdjustedItemPrices {
    [JsonProperty("id")]
    public string ProductId { get; set; }
    [JsonProperty("original_price")]
    public float OriginalPrice { get; set; }
    [JsonProperty("adjusted_price")]
    public float AdjustedPrice { get; set; }
    [JsonProperty("name")]
    public string Name { get; set; }
}

class Item {
    [JsonProperty("id")]
    public string ProductId { get; set; }
    [JsonProperty("name")]
    public string ProductName { get; set; }
    [JsonProperty("price")]
    public float Price { get; set; }
}

[Authorize]
public class OrderController : Controller
{
    private IOrderingService _orderSvc;
    private IBasketService _basketSvc;
    private readonly IIdentityParser<ApplicationUser> _appUserParser;
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
    public IActionResult ApplyCoupon(string couponCode, string orderModelJson)
    {
        var orderModel = Newtonsoft.Json.JsonConvert.DeserializeObject<Order>(orderModelJson);
        // // Assume you have a service to get and update the order
        // var order = _orderService.GetOrder();

        // // Apply a 10% discount to each item in the order
        // foreach (var item in order.Items)
        // {
        //     item.Price *= 0.9m;
        // }

        // // Update the order with the discounted prices
        // _orderService.UpdateOrder(order);

        // // Re-render the current page with the updated order
        // return View(order);

        // Console.WriteLine("coupon code is" + couponCode);
        // Console.WriteLine("model.OrderItems.Count is " + orderModel.OrderItems.Count);
        // if (orderModel.OrderItems.Count > 0)
        // {
        //     orderModel.OrderItems = orderModel.OrderItems.Take(1).ToList();
        //     Console.WriteLine("Truncate the list to only 1");
        //     Console.WriteLine("model.OrderItems.Count is " + orderModel.OrderItems.Count + " after truncating");
        // }
        // var serializedJson = Newtonsoft.Json.JsonConvert.SerializeObject(orderModel);
        // Console.WriteLine("Update json: " + serializedJson);

        var request = new ApplyCouponRequest {
            CouponCode = couponCode,
            items = orderModel.OrderItems.Select(item => new Item {
                ProductId = item.ProductId.ToString(),
                ProductName = item.ProductName,
                Price = (float)item.UnitPrice
            }).ToArray()
        };

        var serializedJson = Newtonsoft.Json.JsonConvert.SerializeObject(request);

        Console.WriteLine("Send request: " + serializedJson);
        var client = new HttpClient();
        //var content = new StringContent(serializedJson, Encoding.UTF8, "application/json");

        var webRequest = new HttpRequestMessage(HttpMethod.Post, "http://coupon-api:5000/apply-coupon")
        {
            Content = new StringContent(serializedJson, Encoding.UTF8, "application/json")
        };
        var response = client.Send(webRequest);
        Console.WriteLine("Status code is: " + response.StatusCode);
        // using (var reader = new StreamReader(response.Content.ReadAsStream()))
        // {

        //     Console.WriteLine("Body is: " + reader.ReadToEnd());
        // }
        // if (orderModel.OrderItems.Count > 0)
        // {
        //     orderModel.OrderItems = orderModel.OrderItems.Take(1).ToList();
        // }
        // Console.WriteLine("Truncate the list to only 1");
        // Console.WriteLine("model.OrderItems.Count is " + orderModel.OrderItems.Count + " after truncating");
        
        if (response.StatusCode == System.Net.HttpStatusCode.OK) {
            var responseBody = response.Content.ReadAsStringAsync().Result;
            Console.WriteLine("Body is: " + responseBody);
            var result = Newtonsoft.Json.JsonConvert.DeserializeObject<ApplyCouponResponse>(responseBody);
            // foreach (var item in result.items)
            // {
            //     var orderItem = orderModel.OrderItems.FirstOrDefault(i => i.ProductId.ToString() == item.ProductId);
            //     if (orderItem != null)
            //     {
            //         // orderItem.UnitPrice = item.AdjustedPrice;
            //         orderItem.UnitPrice = (decimal)item.AdjustedPrice;
            //     }
            // }
            orderModel.Total = (decimal)result.FinalPrice;
        }
        return PartialView("_OrderItems", orderModel);
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
