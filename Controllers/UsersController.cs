using System.Security.Claims;
using AIShoppingAssistant.Data;
using AIShoppingAssistant.DTOs;
using AIShoppingAssistant.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIShoppingAssistant.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        ApplicationDbContext context,
        ILogger<UsersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterDto registerDto)
    {
        try
        {
            var normalizedEmail = registerDto.Email.Trim().ToLowerInvariant();
            var emailExists = await _context.Users.AnyAsync(user => user.Email == normalizedEmail);

            if (emailExists)
            {
                return Conflict(new { message = "A user with this email already exists." });
            }

            var user = new User
            {
                FullName = registerDto.FullName.Trim(),
                Email = normalizedEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUserProfile), new { id = user.Id }, new
            {
                success = true,
                message = "Registration successful."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register user with email {Email}.", registerDto.Email);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Registration failed." });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginDto loginDto)
    {
        try
        {
            var normalizedEmail = loginDto.Email.Trim().ToLowerInvariant();
            var user = await _context.Users
                .Include(existingUser => existingUser.UserPreference)
                .SingleOrDefaultAsync(existingUser => existingUser.Email == normalizedEmail);

            if (user is null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid email or password." });
            }

            user.LastLogin = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Login successful."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log in user with email {Email}.", loginDto.Email);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Login failed." });
        }
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<ActionResult<UserProfileDto>> GetUserProfile()
    {
        try
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdValue, out var userId))
            {
                return Unauthorized(new { message = "Invalid authentication session." });
            }

            var user = await _context.Users
                .AsNoTracking()
                .Include(existingUser => existingUser.UserPreference)
                .SingleOrDefaultAsync(existingUser => existingUser.Id == userId);

            if (user is null)
            {
                return NotFound(new { message = "User not found." });
            }

            return Ok(MapUserProfile(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve user profile.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not retrieve user profile." });
        }
    }

    private static UserProfileDto MapUserProfile(User user)
    {
        return new UserProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            LastLogin = user.LastLogin,
            Preferences = user.UserPreference is null
                ? null
                : new UserPreferenceDto
                {
                    FavoriteStyles = user.UserPreference.FavoriteStyles,
                    FavoriteColors = user.UserPreference.FavoriteColors,
                    FavoriteStores = user.UserPreference.FavoriteStores,
                    PreferredPriceRangeMin = user.UserPreference.PreferredPriceRangeMin,
                    PreferredPriceRangeMax = user.UserPreference.PreferredPriceRangeMax
                }
        };
    }
}
