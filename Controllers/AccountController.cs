using System.Security.Claims;
using AIShoppingAssistant.Data;
using AIShoppingAssistant.Models;
using AIShoppingAssistant.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIShoppingAssistant.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private const string ChatSessionCookie = "AIShopping.ChatSession";
    private readonly ApplicationDbContext _context;

    public AccountController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("/Account/Login")]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost("/Account/Login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .SingleOrDefaultAsync(existingUser => existingUser.Email == normalizedEmail, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        user.LastLogin = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var expires = DateTimeOffset.UtcNow.AddHours(model.RememberMe ? 24 * 14 : 2);

        var claims = GetClaims(user);
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = expires
            });

        return RedirectToLocal(returnUrl);
    }

    [HttpGet("/Account/Register")]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost("/Account/Register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var emailExists = await _context.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            ModelState.AddModelError(nameof(model.Email), "A user with this email already exists.");
            return View(model);
        }

        var user = new User
        {
            FullName = model.FullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = "Registration successful. Please sign in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost("/Account/Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await ClearActiveChatSessionsAsync();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("/Account/AccessDenied")]
    public IActionResult AccessDenied()
    {
        return View(nameof(Login), new LoginViewModel());
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    private async Task ClearActiveChatSessionsAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (int.TryParse(userId, out var parsedUserId))
        {
            var sessions = await _context.ChatSessions
                .Where(chatSession => chatSession.UserId == parsedUserId && chatSession.EndedAt == null)
                .ToListAsync();

            if (sessions.Count > 0)
            {
                var sessionIds = sessions.Select(session => session.Id).ToList();
                var messages = _context.ChatMessages.Where(message =>
                    message.UserId == parsedUserId && sessionIds.Contains(message.ChatSessionId));
                _context.ChatMessages.RemoveRange(messages);
                foreach (var session in sessions)
                {
                    session.EndedAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
            }
        }

        Response.Cookies.Delete(ChatSessionCookie, new CookieOptions { Path = "/" });
        Response.Cookies.Delete(ChatSessionCookie, new CookieOptions { Path = "/api/chat" });
    }

    private static IEnumerable<Claim> GetClaims(User user)
    {
        yield return new Claim(ClaimTypes.NameIdentifier, user.Id.ToString());

        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            yield return new Claim(ClaimTypes.Name, user.FullName);
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            yield return new Claim(ClaimTypes.Email, user.Email);
        }
    }
}
