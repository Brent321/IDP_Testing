namespace IDP_Testing.Configuration;

public class FrontendOptions
{
    public const string SectionName = "FrontendMode";
    
    public FrontendMode Mode { get; set; } = FrontendMode.Blazor;
}

public enum FrontendMode
{
    Blazor,
    React
}