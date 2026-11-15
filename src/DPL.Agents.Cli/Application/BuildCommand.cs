using Spectre.Console;

namespace DPL.Agents.Cli.Application;

/// <summary>Handles the build command and its user-facing output.</summary>
internal sealed class BuildCommand(
    BuildConfigurationLoader configurationLoader,
    CopyPlanner planner,
    CopyExecutor executor,
    IAnsiConsole console) : ICommand
{
    private const string Usage = "Usage: dpl build [--config <file>] [--dry-run]";

    public string Name => "build";

    public string Description => "Copy files and folders configured in build.yaml.";

    /// <summary>Parses arguments and runs a configured build.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Zero on success, two for usage errors, or one for build errors.</returns>
    public async Task<int> RunAsync(string[] args)
    {
        var configPath = "build.yaml";
        var dryRun = false;
        var configProvided = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    console.WriteLine(Usage);
                    return 0;
                case "--dry-run" when !dryRun:
                    dryRun = true;
                    break;
                case "--config" when !configProvided && i + 1 < args.Length && !args[i + 1].StartsWith('-'):
                    configPath = args[++i];
                    configProvided = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
                    Console.Error.WriteLine(Usage);
                    return 2;
            }
        }

        try
        {
            var config = await configurationLoader.LoadAsync(configPath).ConfigureAwait(false);
            var operations = await planner.PlanAsync(config).ConfigureAwait(false);
            if (!dryRun)
            {
                await executor.ExecuteAsync(operations).ConfigureAwait(false);
            }

            foreach (var operation in operations)
            {
                var status = operation.IsUnchanged
                    ? "[grey]skip[/]"
                    : dryRun ? "[yellow]would copy[/]" : "[green]copy[/]";
                console.MarkupLine($"{status} {Markup.Escape(operation.Source)} [grey]->[/] {Markup.Escape(operation.Destination)}");
            }

            console.MarkupLine($"[bold]{operations.Count(operation => !operation.IsUnchanged)}[/] file(s) {(dryRun ? "would be copied" : "copied")}; [bold]{operations.Count(operation => operation.IsUnchanged)}[/] unchanged.");
            return 0;
        }
        catch (Exception ex) when (ex is BuildException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or YamlDotNet.Core.YamlException)
        {
            Console.Error.WriteLine($"Build failed: {ex.Message}");
            return 1;
        }
    }
}
