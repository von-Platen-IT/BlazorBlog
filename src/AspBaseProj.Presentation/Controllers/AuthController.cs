using System.Security.Claims;
using AspBaseProj.Application.Auth;
using AspBaseProj.Application.Contracts.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspBaseProj.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AuthService authService,
    OAuthService oauthService) : ControllerBase
{
    /// <summary>
    /// POST /api/auth/register — Register a new user and sign in with cookie.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register()
    {
        string userName, email, password;

        if (Request.HasJsonContentType())
        {
            var body = await Request.ReadFromJsonAsync<RegisterRequest>();
            if (body is null) return BadRequest(new { error = "Invalid request body." });
            userName = body.UserName;
            email = body.Email ?? "";
            password = body.Password;
        }
        else
        {
            var form = await Request.ReadFormAsync();
            userName = form["userName"].ToString();
            email = form["email"].ToString();
            password = form["password"].ToString();
        }

        try
        {
            var response = await authService.RegisterAsync(new RegisterRequest(userName, string.IsNullOrEmpty(email) ? null : email, password));
            await AuthHelper.SignInWithCookie(HttpContext, response);
            return Request.HasJsonContentType() ? Ok(response) : Redirect("/");
        }
        catch (InvalidOperationException ex)
        {
            return Request.HasJsonContentType()
                ? BadRequest(new { error = ex.Message })
                : Redirect($"/register?error={Uri.EscapeDataString(ex.Message)}");
        }
    }

    /// <summary>
    /// POST /api/auth/login — Authenticate user and sign in with cookie.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login()
    {
        string userName, password;

        if (Request.HasJsonContentType())
        {
            var body = await Request.ReadFromJsonAsync<LoginRequest>();
            if (body is null) return BadRequest(new { error = "Invalid request body." });
            userName = body.UserName;
            password = body.Password;
        }
        else
        {
            var form = await Request.ReadFormAsync();
            userName = form["userName"].ToString();
            password = form["password"].ToString();
        }

        try
        {
            var response = await authService.LoginAsync(new LoginRequest(userName, password));
            await AuthHelper.SignInWithCookie(HttpContext, response);
            return Request.HasJsonContentType() ? Ok(response) : Redirect("/");
        }
        catch (UnauthorizedAccessException)
        {
            return Request.HasJsonContentType()
                ? Unauthorized()
                : Redirect("/login?error=Invalid%20credentials");
        }
        catch (InvalidOperationException)
        {
            return Request.HasJsonContentType()
                ? Unauthorized()
                : Redirect("/login?error=Login%20failed");
        }
    }

    /// <summary>
    /// POST /api/auth/logout — Sign out the current user.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Request.HasJsonContentType()
            ? Ok(new { message = "Logged out" })
            : Redirect("/");
    }

    /// <summary>
    /// GET /api/auth/me — Return current user info.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        if (HttpContext.User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        return Ok(new AuthUserInfo(
            HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
            HttpContext.User.FindFirstValue(ClaimTypes.Name) ?? "",
            HttpContext.User.FindFirstValue(ClaimTypes.Email),
            HttpContext.User.FindFirstValue("is_root") == "true",
            HttpContext.User.FindAll("groups").Select(c => c.Value).ToList()
        ));
    }

    /// <summary>
    /// GET /api/auth/login/{provider} — Initiate OAuth challenge.
    /// </summary>
    [HttpGet("login/{provider}")]
    [AllowAnonymous]
    public IActionResult LoginWithProvider(string provider)
    {
        var scheme = AuthHelper.ResolveOAuthScheme(provider);
        if (scheme is null)
            return BadRequest(new { error = $"Unknown provider: {provider}" });

        return Challenge(new AuthenticationProperties
        {
            RedirectUri = $"/api/auth/callback/{provider}"
        }, [scheme]);
    }

    /// <summary>
    /// GET /api/auth/callback/{provider} — OAuth callback handler.
    /// </summary>
    [HttpGet("callback/{provider}")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(string provider)
    {
        var scheme = AuthHelper.ResolveOAuthScheme(provider);
        if (scheme is null)
            return BadRequest(new { error = $"Unknown provider: {provider}" });

        var authResult = await HttpContext.AuthenticateAsync(scheme);
        if (!authResult.Succeeded)
            return Unauthorized();

        var claims = authResult.Principal;
        var providerKey = claims.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var email = claims.FindFirstValue(ClaimTypes.Email);
        var name = claims.FindFirstValue(ClaimTypes.Name);

        var response = await oauthService.LoginOrRegisterFromOAuthAsync(provider, providerKey, email, name);
        await AuthHelper.SignInWithCookie(HttpContext, response);
        return Redirect("/");
    }
}