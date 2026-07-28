using AIShopping.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIShoppingAssistant.Controllers;

[Authorize]
public class ShoppingController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ShoppingController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("/Shopping/Assistant")]
    public IActionResult Assistant()
    {
        return View();
    }

    [HttpPost("/Shopping/Search")]
    public async Task<IActionResult> Search([FromBody] ShoppingQueryDto request, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var apiUrl = $"{Request.Scheme}://{Request.Host}/api/shopping/assistant";
        ForwardRequestCookies(client);

        var response = await client.PostAsJsonAsync(apiUrl, request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return new ContentResult
        {
            Content = body,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            StatusCode = (int)response.StatusCode
        };
    }

    private void ForwardRequestCookies(HttpClient client)
    {
        if (Request.Headers.TryGetValue("Cookie", out var cookies))
        {
            client.DefaultRequestHeaders.Add("Cookie", cookies.ToString());
        }
    }
}
