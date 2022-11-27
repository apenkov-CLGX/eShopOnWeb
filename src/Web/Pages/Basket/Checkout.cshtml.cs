using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using BlazorAdmin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Web.Interfaces;

namespace Microsoft.eShopWeb.Web.Pages.Basket;

[Authorize]
public class CheckoutModel : PageModel
{
    private readonly IBasketService _basketService;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IOrderService _orderService;
    private string? _username = null;
    private readonly IBasketViewModelService _basketViewModelService;
    private readonly IAppLogger<CheckoutModel> _logger;
    private readonly HttpClient _httpClient;

    public CheckoutModel(IBasketService basketService,
        IBasketViewModelService basketViewModelService,
        SignInManager<ApplicationUser> signInManager,
        IOrderService orderService,
        IAppLogger<CheckoutModel> logger,
        HttpClient httpClient)
    {
        _basketService = basketService;
        _signInManager = signInManager;
        _orderService = orderService;
        _basketViewModelService = basketViewModelService;
        _logger = logger;
        _httpClient = httpClient;
    }

    public BasketViewModel BasketModel { get; set; } = new BasketViewModel();

    public async Task OnGet()
    {
        await SetBasketModelAsync();
    }

    public async Task<IActionResult> OnPost(IEnumerable<BasketItemViewModel> items)
    {
        try
        {
            await SetBasketModelAsync();

            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var updateModel = items.ToDictionary(b => b.Id.ToString(), b => b.Quantity);
            await _basketService.SetQuantities(BasketModel.Id, updateModel);
            var address = new Address("123 Main St.", "Kent", "OH", "United States", "44240");
            await _orderService.CreateOrderAsync(BasketModel.Id, address);
            await _basketService.DeleteBasketAsync(BasketModel.Id);

            //await CallAzureFunctionOrderItemsReserverAsync(BasketModel.Id, items.Sum(o => o.Quantity));
            await SendOrderItemsReserverMessageAsync(BasketModel.Id, items.Sum(o => o.Quantity));
            await CallAzureFunctionDeliveryOrderProcessorAsync(BasketModel.Id, address, items);
        }
        catch (EmptyBasketOnCheckoutException emptyBasketOnCheckoutException)
        {
            //Redirect to Empty Basket page
            _logger.LogWarning(emptyBasketOnCheckoutException.Message);
            return RedirectToPage("/Basket/Index");
        }

        return RedirectToPage("Success");
    }

    private async Task SetBasketModelAsync()
    {
        if (_signInManager.IsSignedIn(HttpContext.User))
        {
            BasketModel = await _basketViewModelService.GetOrCreateBasketForUser(User.Identity.Name);
        }
        else
        {
            GetOrSetBasketCookieAndUserName();
            BasketModel = await _basketViewModelService.GetOrCreateBasketForUser(_username);
        }
    }

    private void GetOrSetBasketCookieAndUserName()
    {
        if (Request.Cookies.ContainsKey(Constants.BASKET_COOKIENAME))
        {
            _username = Request.Cookies[Constants.BASKET_COOKIENAME];
        }
        if (_username != null) return;

        _username = Guid.NewGuid().ToString();
        var cookieOptions = new CookieOptions();
        cookieOptions.Expires = DateTime.Today.AddYears(10);
        Response.Cookies.Append(Constants.BASKET_COOKIENAME, _username, cookieOptions);
    }

    private async Task CallAzureFunctionOrderItemsReserverAsync(int itemId, int quantity)
    {
        var option = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var json = JsonSerializer.Serialize(
            new { ItemId = itemId, Quantity = quantity },
            option);

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        await _httpClient.PostAsync(
            "https://orderitemsreserver-apenkov.azurewebsites.net/api/OrderItemsReserver?code=WPeV4IdaR4DwLowBxUbegyHplRprMttTc-ekw2S7YCt8AzFus5Kd8w==",
            content
        );
    }

    private async Task SendOrderItemsReserverMessageAsync(int itemId, int quantity)
    {
        var option = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var json = JsonSerializer.Serialize(
            new { ItemId = itemId, Quantity = quantity },
            option);

        await using var client = new ServiceBusClient("Endpoint=sb://eshop-apenkov.servicebus.windows.net/;SharedAccessKeyName=BusPolicy;SharedAccessKey=Z/pWb2s0plZv0DTplxr87PdBz3wvm8JylftmxBjrf0k=;");

        await using ServiceBusSender sender = client.CreateSender("orderitemsreserverqueue");
        var message = new ServiceBusMessage(json);
        
        _logger.LogInformation($"Sending message: {json}");
        await sender.SendMessageAsync(message);
    }

    private async Task CallAzureFunctionDeliveryOrderProcessorAsync(int id, Address address, IEnumerable<BasketItemViewModel> items)
    {
        var option = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var json = JsonSerializer.Serialize(
            new 
            { 
                id = $"Order-{id}",
                ShippingAddress = address,
                Items = items.Select(i => new
                {
                    ProductName = i.ProductName,
                    UnitPrice = i.UnitPrice,
                    Quantity = i.Quantity
                }),
                FinalPrice = items.Sum(i => i.UnitPrice * (decimal)i.Quantity)
            },
            option);

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        await _httpClient.PostAsync(
            "https://functions-apenkov.azurewebsites.net/api/DeliveryOrderProcessor?code=uYnIEg446tfpiQUKnujnA_V3Tb1Ve_qrclYmGb-baoM0AzFuv-8oeg==",
            content
        );
    }
}
