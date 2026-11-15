using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DPL.Agents.Cli.Application;

internal sealed class BuildConfigurationLoader
{
    public async Task<LoadedConfiguration> LoadAsync(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new BuildException("The config path cannot be empty.");
        }

        var absolutePath = Path.GetFullPath(configPath);
        if (!File.Exists(absolutePath))
        {
            throw new BuildException($"Config file not found: {absolutePath}");
        }

        var yaml = await File.ReadAllTextAsync(absolutePath).ConfigureAwait(false);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var configuration = deserializer.Deserialize<BuildConfiguration>(yaml)
            ?? throw new BuildException("The config file is empty.");
        if (configuration.Version != 1)
        {
            throw new BuildException($"Unsupported config version: {configuration.Version}. Expected 1.");
        }

        if (configuration.Steps is not { Count: > 0 })
        {
            throw new BuildException("The config must contain at least one step.");
        }

        return new LoadedConfiguration(Path.GetDirectoryName(absolutePath)!, configuration);
    }
}
