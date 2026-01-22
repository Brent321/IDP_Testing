using System.Diagnostics;

namespace IDP_Testing.Services;

public class ReactDevelopmentServer : IHostedService, IDisposable
{
    private readonly ILogger<ReactDevelopmentServer> _logger;
    private readonly IHostEnvironment _environment;
    private Process? _npmProcess;

    public ReactDevelopmentServer(ILogger<ReactDevelopmentServer> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return Task.CompletedTask;
        }

        try
        {
            var workingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..", "react-front-end");
            
            var startInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd" : "npm",
                Arguments = OperatingSystem.IsWindows() ? "/c npm run dev" : "run dev",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Inject environment variables to avoid opening browser
            startInfo.Environment["BROWSER"] = "none";

            _logger.LogInformation("Starting React Development Server in '{WorkingDirectory}'...", workingDirectory);

            _npmProcess = new Process { StartInfo = startInfo };
            
            _npmProcess.OutputDataReceived += (sender, e) => 
            {
                if (!string.IsNullOrEmpty(e.Data)) _logger.LogInformation("[React] {Output}", e.Data);
            };
            
            _npmProcess.ErrorDataReceived += (sender, e) => 
            {
                if (!string.IsNullOrEmpty(e.Data)) _logger.LogError("[React] {Error}", e.Data);
            };

            _npmProcess.Start();
            _npmProcess.BeginOutputReadLine();
            _npmProcess.BeginErrorReadLine();

            _logger.LogInformation("React Development Server started.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start React Development Server.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_npmProcess != null && !_npmProcess.HasExited)
        {
            _logger.LogInformation("Stopping React Development Server...");
            try
            {
                // On Windows, killing the cmd process might not kill the child node process
                // Ideally we would kill the process tree, but for dev usage this is often sufficient
                // or users can manually stop node.
                _npmProcess.Kill(true); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping React Development Server.");
            }
            _npmProcess.Dispose();
            _npmProcess = null;
        }
    }
}
