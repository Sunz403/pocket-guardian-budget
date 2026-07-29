using AIShoppingAssistant.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIShoppingAssistant.Controllers;

[Authorize]
public sealed class StoreLocatorController : Controller
{
    private readonly StoreService _stores;
    public StoreLocatorController(StoreService stores) => _stores = stores;

    [HttpGet("/Stores")]
    public IActionResult Index() => View(_stores.Categories);

    [HttpGet("/Stores/Data")]
    public IActionResult Data(string? category) => Json(_stores.GetStores(category).Select(store => new
    {
        store.Id, store.Name, store.Address, store.Latitude, store.Longitude, store.Category
    }));
}
