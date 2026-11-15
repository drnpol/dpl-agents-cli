using DPL.Agents.Cli.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace DPL.Agents.Cli;

/// <summary>Configures and starts the dpl command-line tool.</summary>
public static class Program
{
    /// <summary>Runs the requested command.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Zero on success, two for usage errors, or one for build errors.</returns>
    public static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);
        builder.Services.AddSingleton<ICommand, BuildCommand>();
        builder.Services.AddSingleton<CommandDispatcher>();
        builder.Services.AddSingleton<BuildConfigurationLoader>();
        builder.Services.AddSingleton<CopyPlanner>();
        builder.Services.AddSingleton<CopyExecutor>();

        using var host = builder.Build();
        return await host.Services.GetRequiredService<CommandDispatcher>().RunAsync(args).ConfigureAwait(false);
    }
}
