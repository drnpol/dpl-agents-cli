using Microsoft.Extensions.Logging;
using DPL.Agents.Cli;

public class Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<Application> _logger;

    public Application(
        IServiceProvider serviceProvider,
        ILogger<Application> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void Run()
    {
        _logger.LogInformation("Application is running...");
    }
}