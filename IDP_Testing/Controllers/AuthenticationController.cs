using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.WsFederation;
using Microsoft.AspNetCore.Mvc;
using Sustainsys.Saml2.AspNetCore2;

namespace IDP_Testing.Controllers;

[Route("authentication")]
public class AuthenticationController : Controller
{
    [HttpGet("login")]
    public IActionResult Login(string? authMode = null, string? returnUrl = null)
    {
        // Determine which authentication scheme to use
        var scheme = authMode?.ToLower() switch
        {
            "oidc" or "keycloak" => OpenIdConnectDefaults.AuthenticationScheme,
            "saml" or "saml2" => Saml2Defaults.Scheme,
            "wsfed" or "adfs" => WsFederationDefaults.AuthenticationScheme,
            _ => OpenIdConnectDefaults.AuthenticationScheme
        };

        var properties = new AuthenticationProperties
        {
            RedirectUri = returnUrl ?? Url.Content("~/")
        };

        return Challenge(properties, scheme);
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        // Prevent double-processing: if user is not authenticated, just redirect
        if (User.Identity?.IsAuthenticated != true)
        {
            Console.WriteLine("Logout called but user not authenticated - redirecting home");
            return Redirect("~/");
        }

        // Get the id_token BEFORE any sign out
        var idToken = await HttpContext.GetTokenAsync("id_token");
        Console.WriteLine($"id_token before logout: {(string.IsNullOrEmpty(idToken) ? "NULL" : "present")}");
        
        var authenticationType = User.Identity?.AuthenticationType;
        Console.WriteLine($"Authentication type: {authenticationType}");

        // Determine which scheme was used for authentication
        var scheme = authenticationType switch
        {
            "AuthenticationTypes.Federation" or "WsFederation" => WsFederationDefaults.AuthenticationScheme,
            "Saml2" => Saml2Defaults.Scheme,
            _ => OpenIdConnectDefaults.AuthenticationScheme
        };

        // Build authentication properties with the id_token
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Content("~/")
        };

        // Store the id_token in Items for OIDC logout
        if (scheme == OpenIdConnectDefaults.AuthenticationScheme && !string.IsNullOrWhiteSpace(idToken))
        {
            properties.Items["id_token"] = idToken;
            Console.WriteLine("Stored id_token in properties.Items");
        }

        return SignOut(properties, scheme, CookieAuthenticationDefaults.AuthenticationScheme);
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
}