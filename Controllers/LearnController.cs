using Microsoft.AspNetCore.Mvc;

namespace AIShoppingAssistant.Controllers;

public class LearnController : Controller
{
    [HttpGet("/Learn/Index")]
    public IActionResult Index() => View();
}
