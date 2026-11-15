using System.Reflection;
using Spectre.Console;

namespace DPL.Agents.Cli.Application;

internal sealed class CommandDispatcher(IEnumerable<ICommand> commands, IAnsiConsole console)
{
    public async Task<int> RunAsync(string[] args)
    {
        var availableCommands = commands.ToDictionary(command => command.Name, StringComparer.OrdinalIgnoreCase);
        if (args.Length == 1 && args[0] == "--version")
        {
            var version = typeof(CommandDispatcher).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion.Split('+')[0] ?? "unknown";
            console.WriteLine($"dpl {version}");
            return 0;
        }

        if (args.Length == 1 && args[0] is "--help" or "-h" or "help")
        {
            WriteHelp(availableCommands.Values);
            return 0;
        }

        if (args.Length == 0)
        {
            WriteHelp(availableCommands.Values);
            return 2;
        }

        if (!availableCommands.TryGetValue(args[0], out var command))
        {
            Console.Error.WriteLine($"Unknown command: {args[0]}");
            WriteHelp(availableCommands.Values);
            return 2;
        }

        return await command.RunAsync(args[1..]).ConfigureAwait(false);
    }

    private void WriteHelp(IEnumerable<ICommand> availableCommands)
    {
        console.WriteLine("Usage: dpl <command> [options]");
        console.WriteLine("       dpl --version");
        console.WriteLine();
        console.MarkupLine("[bold]Commands[/]");
        foreach (var command in availableCommands.OrderBy(command => command.Name, StringComparer.Ordinal))
        {
            console.MarkupLine($"  [green]{Markup.Escape(command.Name)}[/]  {Markup.Escape(command.Description)}");
        }

        console.WriteLine();
        console.WriteLine("Run dpl <command> --help for command options.");
    }
}
