using Microsoft.Playwright;

namespace IDP_Testing.PlaywrightTests
{
    [Parallelizable(ParallelScope.Self)]
    [TestFixture]
    public class PluginUploadE2ETest : PageTest
    {
        private const string BaseUrl = "https://localhost:7235";
        private const string AdminPluginsUrl = $"{BaseUrl}/admin/plugins";
        
        // Test credentials - update these based on your Keycloak user with app-admin role
        private const string AdminUsername = "bob";
        private const string AdminPassword = "Pass123$";

        [SetUp]
        public async Task SetUp()
        {
            Page.SetDefaultTimeout(30000);
            
            // Capture console messages from the browser
            Page.Console += (_, msg) =>
            {
                Console.WriteLine($"[BROWSER {msg.Type}] {msg.Text}");
            };
            
            // Capture page errors
            Page.PageError += (_, error) =>
            {
                Console.WriteLine($"[PAGE ERROR] {error}");
            };
        }

        [Test]
        public async Task CompletePluginUploadWorkflow()
        {
            try
            {
                // Find the compiled test plugin DLL
                var testDllPath = FindTestPluginDll();
                
                if (string.IsNullOrEmpty(testDllPath))
                {
                    Assert.Fail("Test plugin DLL not found. Please build the TestBlazorPlugin project first.\n" +
                               "Run: dotnet build TestBlazorPlugin/TestBlazorPlugin.csproj");
                    return;
                }

                Console.WriteLine($"Using test plugin from: {testDllPath}");

                // Step 1: Navigate to application home page
                Console.WriteLine("\n[Step 1] Navigating to application...");
                var response = await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
                Console.WriteLine($"  Response status: {response?.Status}");
                Console.WriteLine($"  Current URL: {Page.Url}");
                Console.WriteLine("[PASS] Navigated to application");

                // Step 2: Authenticate with Keycloak
                Console.WriteLine("\n[Step 2] Authenticating with Keycloak...");
                await AuthenticateWithKeycloak();
                Console.WriteLine("[PASS] Authenticated successfully");

                // Step 3: Verify we're logged in and can see admin link
                Console.WriteLine("\n[Step 3] Verifying admin access...");
                var adminNavLink = Page.Locator("a.nav-link[href='admin']");
                await Expect(adminNavLink).ToBeVisibleAsync(new() { Timeout = 5000 });
                Console.WriteLine("[PASS] Admin nav link visible - user has admin role");

                // Step 4: Navigate to plugin management page
                Console.WriteLine("\n[Step 4] Navigating to plugin management page...");
                await Page.GotoAsync(AdminPluginsUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
                Console.WriteLine($"  Current URL: {Page.Url}");
                
                var heading = Page.Locator("h1:has-text('Plugin Management')");
                await Expect(heading).ToBeVisibleAsync(new() { Timeout = 5000 });
                Console.WriteLine("[PASS] Plugin Management page loaded");

                // Step 5: Get initial plugin count
                Console.WriteLine("\n[Step 5] Counting initial plugins...");
                var initialCount = await GetLoadedAssemblyCount();
                Console.WriteLine($"[PASS] Initial plugin count: {initialCount}");

                // Step 6: Upload the test plugin
                Console.WriteLine("\n[Step 6] Uploading test plugin...");
                var fileInput = Page.Locator("input[type='file'][accept='.dll']");

                // Set the files
                await fileInput.SetInputFilesAsync(testDllPath);
                Console.WriteLine("[INFO] Files set on input element");

                // Wait a moment for Blazor to detect the file
                await Page.WaitForTimeoutAsync(500);

                // Check if Blazor detected the change
                var uploadingIndicator = Page.Locator(".progress-bar-animated:has-text('Uploading')");
                var hasUploadingIndicator = await uploadingIndicator.IsVisibleAsync();
                Console.WriteLine($"[INFO] Upload indicator visible: {hasUploadingIndicator}");

                if (!hasUploadingIndicator)
                {
                    // If no indicator appeared, Blazor might not have detected the change
                    // Try manually triggering the input event
                    Console.WriteLine("[INFO] Manually triggering input change event");
                    await fileInput.EvaluateAsync(@"(element) => {
                        const event = new Event('change', { bubbles: true });
                        element.dispatchEvent(event);
                    }");
                    await Page.WaitForTimeoutAsync(500);
                }

                Console.WriteLine("[PASS] File upload initiated");

                // Step 7: Wait for upload to complete and check for result
                Console.WriteLine("\n[Step 7] Waiting for upload to complete...");

                // Wait up to 20 seconds for either success or error
                var errorAlert = Page.Locator(".alert-danger");
                var successAlert = Page.Locator(".alert-success");
                var resultLocator = Page.Locator(".alert-success, .alert-danger").First;

                try
                {
                    await Expect(resultLocator).ToBeVisibleAsync(new() { Timeout = 20000 });
                    
                    // Check which one appeared
                    var isError = await errorAlert.IsVisibleAsync();
                    var isSuccess = await successAlert.IsVisibleAsync();
                    
                    if (isError)
                    {
                        var errorText = await errorAlert.InnerTextAsync();
                        Console.WriteLine($"[ERROR] Upload failed with error: {errorText}");
                        
                        // Take diagnostic screenshot
                        await Page.ScreenshotAsync(new()
                        {
                            Path = Path.Combine(TestContext.CurrentContext.TestDirectory, "upload-error.png"),
                            FullPage = true
                        });
                        
                        Assert.Fail($"Upload failed: {errorText}");
                    }
                    else if (isSuccess)
                    {
                        var successText = await successAlert.InnerTextAsync();
                        Console.WriteLine($"  Success message: {successText}");
                        Assert.That(successText, Does.Contain("Successfully uploaded"), 
                            "Success message should confirm upload");
                        Console.WriteLine("[PASS] Upload successful");
                    }
                }
                catch (TimeoutException)
                {
                    Console.WriteLine("[ERROR] No success or error message appeared within 20 seconds");
                    
                    // Take diagnostic screenshot
                    await Page.ScreenshotAsync(new()
                    {
                        Path = Path.Combine(TestContext.CurrentContext.TestDirectory, "no-response-debug.png"),
                        FullPage = true
                    });
                    
                    // Check page state
                    var pageContent = await Page.ContentAsync();
                    var hasFileInput = pageContent.Contains("input", StringComparison.OrdinalIgnoreCase);
                    var hasProgress = pageContent.Contains("Uploading", StringComparison.OrdinalIgnoreCase);
                    var hasAlert = pageContent.Contains("alert", StringComparison.OrdinalIgnoreCase);
                    
                    Console.WriteLine($"  Page has file input: {hasFileInput}");
                    Console.WriteLine($"  Page shows 'Uploading': {hasProgress}");
                    Console.WriteLine($"  Page has alert elements: {hasAlert}");
                    Console.WriteLine($"  Current URL: {Page.Url}");
                    
                    // Check if SignalR is connected
                    var signalRState = await Page.EvaluateAsync<string>(@"() => {
                        return window.Blazor ? 'Blazor loaded' : 'Blazor NOT loaded';
                    }");
                    Console.WriteLine($"  Blazor state: {signalRState}");
                    
                    throw new Exception("Upload did not produce a response. Possible Blazor SignalR connection issue or component not responding.");
                }

                // Step 8: Verify plugin appears in the list
                Console.WriteLine("\n[Step 8] Verifying plugin in list...");
                
                // Wait a moment for UI to update
                await Page.WaitForTimeoutAsync(1000);
                
                var pluginListItem = Page.Locator("li.list-group-item:has-text('TestBlazorPlugin')");
                await Expect(pluginListItem).ToBeVisibleAsync(new() { Timeout = 5000 });
                Console.WriteLine("[PASS] Plugin appears in loaded assemblies list");

                // Step 9: Verify the "Loaded" badge
                Console.WriteLine("\n[Step 9] Checking loaded status...");
                var loadedBadge = pluginListItem.Locator(".badge.bg-success");
                await Expect(loadedBadge).ToBeVisibleAsync();
                
                var badgeText = await loadedBadge.InnerTextAsync();
                Assert.That(badgeText, Is.EqualTo("Loaded"), "Badge should show 'Loaded'");
                Console.WriteLine("[PASS] Plugin shows as 'Loaded'");

                // Step 10: Verify plugin count increased
                Console.WriteLine("\n[Step 10] Verifying plugin count increased...");
                var finalCount = await GetLoadedAssemblyCount();
                Console.WriteLine($"  Final plugin count: {finalCount}");
                
                Assert.That(finalCount, Is.GreaterThan(initialCount), 
                    $"Plugin count should increase. Initial: {initialCount}, Final: {finalCount}");
                Console.WriteLine($"[PASS] Plugin count increased by {finalCount - initialCount}");

                // Step 11: Take a screenshot for documentation
                Console.WriteLine("\n[Step 11] Taking screenshot...");
                var screenshotPath = Path.Combine(
                    TestContext.CurrentContext.TestDirectory, 
                    "plugin-upload-success.png");
                    
                await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
                Console.WriteLine($"[PASS] Screenshot saved to: {screenshotPath}");

                Console.WriteLine("\n========================================");
                Console.WriteLine("[PASS] END-TO-END TEST COMPLETED SUCCESSFULLY");
                Console.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n========================================");
                Console.WriteLine("[FAIL] TEST FAILED");
                Console.WriteLine("========================================");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                Console.WriteLine($"Current URL: {Page.Url}");
                
                // Take failure screenshot
                var failureScreenshotPath = Path.Combine(
                    TestContext.CurrentContext.TestDirectory,
                    $"failure-{DateTime.Now:yyyyMMdd-HHmmss}.png");
                    
                try
                {
                    await Page.ScreenshotAsync(new() { Path = failureScreenshotPath, FullPage = true });
                    Console.WriteLine($"Failure screenshot saved to: {failureScreenshotPath}");
                }
                catch
                {
                    Console.WriteLine("Could not save failure screenshot");
                }
                
                // Re-throw to fail the test
                throw;
            }
        }

        #region Helper Methods

        private async Task AuthenticateWithKeycloak()
        {
            Console.WriteLine("  Checking if already authenticated...");
            
            // Check if already authenticated by looking for the admin nav link
            var adminNavLink = Page.Locator("a.nav-link[href='admin']");
            
            try
            {
                await adminNavLink.WaitForAsync(new() { Timeout = 3000 });
                Console.WriteLine("  Already authenticated!");
                return; // Already authenticated
            }
            catch
            {
                Console.WriteLine("  Not authenticated, proceeding with login...");
            }

            // Find and click login button - use the navbar one specifically to avoid ambiguity
            Console.WriteLine("  Looking for login button...");
            
            // Target the login button in the navbar specifically (the small one in top-right)
            var loginButton = Page.Locator(".navbar .btn-outline-light[href='/authentication/login']");
            
            var isVisible = await loginButton.IsVisibleAsync();
            Console.WriteLine($"  Navbar login button visible: {isVisible}");
            
            if (!isVisible)
            {
                // Fallback: use .First() to handle multiple matches
                loginButton = Page.Locator("a[href='/authentication/login']").First;
                Console.WriteLine("  Using fallback selector with .First()");
            }
            
            await loginButton.ClickAsync();
            Console.WriteLine("  Clicked login button");

            // Wait for navigation
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Console.WriteLine($"  After login click URL: {Page.Url}");

            // Check if we're on Keycloak login page
            if (Page.Url.Contains("localhost:8080") || Page.Url.Contains("signin-oidc"))
            {
                Console.WriteLine("  On Keycloak login page, filling credentials...");
                
                // Wait for login form to be visible
                await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                
                // Fill in Keycloak credentials
                var usernameInput = Page.Locator("input#username, input[name='username']");
                var passwordInput = Page.Locator("input#password, input[name='password']");
                
                await usernameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                await usernameInput.FillAsync(AdminUsername);
                Console.WriteLine($"  Filled username: {AdminUsername}");
                
                await passwordInput.FillAsync(AdminPassword);
                Console.WriteLine("  Filled password");
                
                var submitButton = Page.Locator("input[type='submit'], button[type='submit']");
                await submitButton.ClickAsync();
                Console.WriteLine("  Clicked submit button");

                // Wait for redirect back to application
                Console.WriteLine("  Waiting for redirect back to application...");
                await Page.WaitForURLAsync(url => url.StartsWith(BaseUrl), new() { Timeout = 20000 });
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                Console.WriteLine($"  Redirected to: {Page.Url}");
            }
            else if (Page.Url.StartsWith(BaseUrl))
            {
                Console.WriteLine("  Already on application - authentication may have succeeded");
            }
            else
            {
                throw new Exception($"Unexpected URL after login click: {Page.Url}");
            }
        }

        private async Task<int> GetLoadedAssemblyCount()
        {
            // First check if there's an "No external plugins loaded" message
            var noPluginsMessage = Page.Locator(".alert-info:has-text('No external plugins loaded')");
            
            if (await noPluginsMessage.IsVisibleAsync())
            {
                return 0;
            }
            
            var assemblyListItems = Page.Locator("ul.list-group > li.list-group-item");
            var count = await assemblyListItems.CountAsync();
            return count;
        }

        private string FindTestPluginDll()
        {
            // Get the test project directory
            var testProjectDir = TestContext.CurrentContext.TestDirectory;
            Console.WriteLine($"Test project directory: {testProjectDir}");
            
            // Search for the compiled TestBlazorPlugin DLL
            var searchPaths = new[]
            {
                // Relative to test output directory
                Path.Combine(testProjectDir, "..", "..", "..", "..", "TestBlazorPlugin", "bin", "Debug", "net10.0", "TestBlazorPlugin.dll"),
                Path.Combine(testProjectDir, "..", "..", "..", "..", "TestBlazorPlugin", "bin", "Release", "net10.0", "TestBlazorPlugin.dll"),
                
                // Absolute path attempts
                @"C:\Users\siebe\source\repos\IDP_Testing\TestBlazorPlugin\bin\Debug\net10.0\TestBlazorPlugin.dll",
                @"C:\Users\siebe\source\repos\IDP_Testing\TestBlazorPlugin\bin\Release\net10.0\TestBlazorPlugin.dll",
            };

            foreach (var path in searchPaths)
            {
                try
                {
                    var fullPath = Path.GetFullPath(path);
                    Console.WriteLine($"Checking: {fullPath}");
                    
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error checking path {path}: {ex.Message}");
                }
            }

            // Try to find it by searching upwards
            var currentDir = new DirectoryInfo(testProjectDir);
            while (currentDir != null && currentDir.Parent != null)
            {
                var pluginPath = Path.Combine(currentDir.FullName, "TestBlazorPlugin", "bin", "Debug", "net10.0", "TestBlazorPlugin.dll");
                if (File.Exists(pluginPath))
                {
                    return pluginPath;
                }
                currentDir = currentDir.Parent;
            }

            return string.Empty;
        }

        #endregion
    }
}