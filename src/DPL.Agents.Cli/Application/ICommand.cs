namespace DPL.Agents.Cli.Application;

internal interface ICommand
{
    string Name { get; }
    string Description { get; }
    Task<int> RunAsync(string[] args);
}
