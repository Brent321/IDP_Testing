using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using IDP_Testing.Configuration;
using Microsoft.AspNetCore.Authorization;

namespace IDP_Testing.Controllers;

[Route("authentication")]
public class AuthenticationController : Controller
{
    private readonly string _authenticationScheme;
    private readonly ILogger<AuthenticationController> _logger;

    public AuthenticationController(IOptions<AuthenticationOptions> authOptions, ILogger<AuthenticationController> logger)
    {
        // The DefaultChallengeScheme is set based on the configured mode
        _authenticationScheme = authOptions.Value.DefaultChallengeScheme 
            ?? throw new InvalidOperationException("No authentication scheme configured");
        _logger = logger;
    }

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = returnUrl ?? Url.Content("~/")
        };

        _logger.LogInformation("Login initiated with scheme: {Scheme}", _authenticationScheme);

        return Challenge(properties, _authenticationScheme);
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            _logger.LogInformation("Logout called but user not authenticated - redirecting home");
            return Redirect(returnUrl ?? "~/");
        }

        if (!string.IsNullOrEmpty(returnUrl))
        {
             // Store the return URL in a cookie so we can access it after the redirect from IDP
             Response.Cookies.Append("PostLogoutReturnUrl", returnUrl, new CookieOptions 
             { 
                 Expires = DateTimeOffset.Now.AddMinutes(10),
                 HttpOnly = true,
                 Secure = true, // We are on https
                 SameSite = SameSiteMode.Lax
             });
        }

        _logger.LogInformation("Logout initiated with scheme: {Scheme}", _authenticationScheme);

        // Get the id_token for OIDC logout
        var idToken = await HttpContext.GetTokenAsync("id_token");
        _logger.LogDebug("id_token before logout: {IdTokenStatus}", string.IsNullOrEmpty(idToken) ? "NULL" : "present");

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Content("~/")
        };

        // Store the id_token for OIDC logout
        if (!string.IsNullOrWhiteSpace(idToken))
        {
            properties.Items["id_token"] = idToken;
            _logger.LogDebug("Stored id_token in properties.Items");
        }

        return SignOut(properties, _authenticationScheme, CookieAuthenticationDefaults.AuthenticationScheme);
    }

    [HttpGet("signout-callback-oidc")]
    public IActionResult SignoutCallbackOidc()
    {
        return Redirect("~/");
    }

    [HttpGet("signout-callback-wsfed")]
    public IActionResult SignoutCallbackWsFed()
    {
        return Redirect("~/");
    }

    [HttpGet("/logged-out")]
    [AllowAnonymous]
    public IActionResult LoggedOut()
    {
         var returnUrl = Request.Cookies["PostLogoutReturnUrl"];
         if (!string.IsNullOrEmpty(returnUrl))
         {
             Response.Cookies.Delete("PostLogoutReturnUrl");
             return Redirect(returnUrl);
         }
         return Redirect("~/");
    }
}