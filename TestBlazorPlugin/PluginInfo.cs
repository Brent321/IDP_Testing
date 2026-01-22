namespace TestBlazorPlugin;

/// <summary>
/// Provides metadata about this plugin
/// </summary>
public static class PluginInfo
{
    public const string Name = "Test Blazor Plugin";
    public const string Version = "1.0.0";
    public const string Author = "Test Author";
    public const string Description = "A test plugin for E2E testing of dynamic component loading";
    
    /// <summary>
    /// Gets all available component types in this plugin
    /// </summary>
    public static Type[] GetAvailableComponents()
    {
        return new[]
        {
            typeof(TestComponent),
            typeof(WeatherWidget)
        };
    }
}