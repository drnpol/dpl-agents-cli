namespace DPL.Agents.Cli.Application;

internal sealed class BuildConfiguration
{
    public int Version { get; set; }
    public List<BuildStep>? Steps { get; set; }
}

internal sealed class BuildStep
{
    public string? Name { get; set; }
    public string? Action { get; set; }
    public string? Source { get; set; }
    public List<string>? Destinations { get; set; }
}

internal sealed record LoadedConfiguration(string RootDirectory, BuildConfiguration Configuration);

internal sealed record CopyOperation(string Source, string Destination, bool IsUnchanged);

internal sealed class BuildException(string message) : Exception(message);
