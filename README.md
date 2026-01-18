# IDP Testing Application

A Blazor Server application demonstrating multiple authentication protocols: **OpenID Connect (OIDC)**, **SAML 2.0**, and **WS-Federation**.

## Features

- **Three authentication methods** running simultaneously
- **Keycloak** for OIDC and SAML 2.0 (runs in Docker)
- **Azure AD** for WS-Federation testing (free tier)
- **Configuration management** UI for runtime config changes
- **Database-backed configuration** overrides

---

## Quick Start

### 1. Prerequisites

- .NET 10 SDK
- Docker Desktop
- Visual Studio 2022+ (or VS Code)
- Azure AD account (free - optional, only for WS-Fed testing)

### 2. Start Keycloak

Keycloak runs at: http://localhost:8080 (admin/admin)

### 3. Configure User Secrets

**Right-click** the `IDP_Testing` project → **"Manage User Secrets"**

Add this configuration:

```json
{
  "Keycloak": {
    "Authority": "http://localhost:8080/realms/your-realm",
    "Audience": "your-audience",
    "ClientId": "blazor-app",
    "ClientSecret": "your-client-secret",
    "ResponseType": "code",
    "Scope": "openid profile email"
  },
  "SAML": {
    "SPOptions": {
      "EntityID": "http://localhost:5000/Saml2",
      "ReturnURL": "http://localhost:5000/signin-saml",
      "Certificate": {
        "File": "saml2.pfx",
        "Password": "your-pfx-password"
      }
    },
    "IdentityProviders": [
      {
        "EntityId": "http://localhost:8080/realms/your-realm",
        "MetadataLocation": "http://localhost:8080/realms/your-realm/protocols/saml/metadata",
        "AllowUnsolicitedAuthnResponse": true
      }
    ]
  }
}
```

For example, with Personal Access Token (PAT) Authorization:
Invoke-RestMethod -X POST -H "Authorization: Bearer {PAT}" -H "Content-Type: application/json" -d "
{
  'Keycloak': {
    'Authority': 'http://localhost:8080/realms/your-realm',
    'Audience': 'your-audience',
    'ClientId': 'blazor-app',
    'ClientSecret': 'your-client-secret',
    'ResponseType': 'code',
    'Scope': 'openid profile email'
  },
  'SAML': {
    'SPOptions': {
      'EntityID': 'http://localhost:5000/Saml2',
      'ReturnURL': 'http://localhost:5000/signin-saml',
      'Certificate': {
        'File': 'saml2.pfx',
        'Password': 'your-pfx-password'
      }
    },
    'IdentityProviders': [
      {
        'EntityId': 'http://localhost:8080/realms/your-realm',
        'MetadataLocation': 'http://localhost:8080/realms/your-realm/protocols/saml/metadata',
        'AllowUnsolicitedAuthnResponse': true
      }
    ]
  }
}" http://localhost:5000/config

**Note:** Replace the placeholders (`your-realm`, `your-audience`, `your-client-secret`, `saml2.pfx`, `your-pfx-password`) with your actual values.

**Getting Keycloak Client Secret:**
1. Open http://localhost:8080
2. Login: `admin` / `admin`
3. Go to: Clients → `blazor-app` → Credentials tab
4. Copy the **Client Secret**

**Getting Azure AD Configuration (Optional - for WS-Fed):**
1. Go to https://portal.azure.com
2. Navigate to: Microsoft Entra ID → Overview
3. Copy the **Directory (tenant) ID**
4. Replace `YOUR-TENANT-ID` in the user secrets above

### 4. Run the Application

Or press **F5** in Visual Studio.

Navigate to: https://localhost:7235

---

## Azure AD Setup (Optional - WS-Fed Only)

### Create App Registration

1. Go to https://portal.azure.com
2. Navigate to: **Microsoft Entra ID** → **App registrations** → **New registration**
3. Configure:
   - **Name**: `IDP Testing` (or any name you prefer)
   - **Account type**: `Accounts in this organizational directory only (Single tenant)`
   - **Redirect URI**: `Web` → `https://localhost:7235/signin-wsfed`
4. Click **Register**

### Configure Application ID URI

After creating the app registration:

1. Go to **Expose an API** in the left menu
2. Click **"Set"** or **"Add"** next to **Application ID URI**
3. Azure will suggest: `api://12345678-1234-1234-1234-123456789abc`
4. **Keep the default** - Click **"Save"**

> **Note:** Azure AD requires the Application ID URI to use a verified domain. Since `https://localhost:7235/` is not a verified domain, you must use the `api://[client-id]` format.

### Get Your Application (Client) ID

1. Go to your app registration → **Overview**
2. Copy the **Application (client) ID** (e.g., `1f0c944e-fa13-46fb-842e-0dc6c6843abc`)

### Update appsettings.json

Edit `appsettings.json` and replace the `Wtrealm` value:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "your-domain.onmicrosoft.com",
    "TenantId": "YOUR-TENANT-ID",
    "ClientId": "1f0c944e-fa13-46fb-842e-0dc6c6843abc",
    "ClientSecret": "your-client-secret",
    "CallbackPath": "/signin-wsfed",
    "Wtrealm": "api://1f0c944e-fa13-46fb-842e-0dc6c6843abc"
  }
}
```

> **Important:** The `Wtrealm` value must **exactly match** the Application ID URI you set in Azure AD.

### Add Front-channel Logout URL

1. In your app registration, go to **Authentication**
2. Scroll to **Front-channel logout URL**
3. Add: `https://localhost:7235/signout-wsfed`
4. Click **Save**

### Add Tenant ID to User Secrets

Add your Azure AD tenant ID to user secrets:

````````
a489b90d-530c-404e-8454-3ed46abb7bb0
````````

Replace `a489b90d-530c-404e-8454-3ed46abb7bb0` with your actual tenant ID.

### Create Test Users (Optional)

1. In Azure Portal, go to **Microsoft Entra ID** → **Users**
2. Click **New user** → **Create new user**
3. Create a test account
4. Assign appropriate roles if needed

**Cost:** Free forever (Azure AD Free tier)

---

## Authentication Methods

| Method | Provider | Port | Test Credentials |
|--------|----------|------|------------------|
| **OIDC** | Keycloak | 8080 | Configured in Keycloak |
| **SAML 2.0** | Keycloak | 8080 | Configured in Keycloak |
| **WS-Fed** | Azure AD | N/A | Your Azure AD account |

---

## Configuration Summary

### appsettings.json (Base Configuration)
- Contains structure and non-sensitive defaults
- **Committed to Git**
- Secrets are empty strings

### User Secrets (Development)
- Contains sensitive values (client secrets, tenant IDs)
- **NOT committed to Git**
- Located at: `%APPDATA%\Microsoft\UserSecrets\997056bc-add4-45a7-941e-d792936b22b5\secrets.json`

### WS-Federation Configuration

The `Wtrealm` parameter identifies your application to Azure AD:

- ✅ **Use:** `api://[client-id]` - Works with Azure AD free tier
- ❌ **Don't use:** `https://localhost:7235/` - Azure AD requires verified domains

**Why?** Azure AD only accepts Application ID URIs that are either:
- The default `api://[client-id]` format
- A verified domain you own (requires Premium)

---

## Configuration Management

Access the admin panel at: https://localhost:7235/admin/configuration

**Requires:** `app-admin` role

Features:
- View active configuration with pagination
- Create database configuration overrides
- Search and filter settings
- Reload configuration at runtime
- Reset to default values

---

## Project Structure

---

## Database

The app uses **SQL Server LocalDB** to store configuration overrides.

**Create/Update Database:**

````````

**Connection String:** Configured in `appsettings.json` (uses integrated auth)

### Migrations

EF Core migrations are used to update the database schema:

- **Add Migration:** `Add-Migration MigrationName`
- **Update Database:** `Update-Database`

---

## Troubleshooting

### "Application with identifier 'https://localhost:7235/' was not found"
- **Cause:** Application ID URI not configured in Azure AD, or doesn't match `Wtrealm`
- **Fix:** 
  1. Set Application ID URI in Azure AD → Expose an API to `api://[client-id]`
  2. Update `appsettings.json` → `Wtrealm` to match: `api://[client-id]`

### "Failed to update Application ID URI... must use a verified domain"
- **Cause:** Trying to use `https://localhost:7235/` as Application ID URI
- **Fix:** Use the default `api://[client-id]` format instead

### "No such host is known" Error
- **Cause:** WS-Fed metadata URL is invalid or tenant ID is wrong
- **Fix:** Verify tenant ID in user secrets matches Azure Portal

### "Client secret is required"
- **Cause:** Missing Keycloak client secret in user secrets
- **Fix:** Right-click project → Manage User Secrets → Add Keycloak:ClientSecret

### Changes to secrets.json not applying
- **Fix:** Restart the application after modifying user secrets

---

## Security Best Practices

✅ **DO:**
- Store all secrets in User Secrets or environment variables
- Keep `appsettings.json` free of sensitive data
- Use `api://[client-id]` format for Azure AD Application ID URI
- Commit cleaned `appsettings.json` to Git

❌ **DON'T:**
- Commit secrets to source control
- Share your `secrets.json` file
- Use `https://localhost` as Application ID URI
- Hard-code credentials in source code

---

## License

MIT License - See LICENSE file for details
