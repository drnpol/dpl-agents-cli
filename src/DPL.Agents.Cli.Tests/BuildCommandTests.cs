using System.Diagnostics;
using Xunit;

namespace DPL.Agents.Cli.Tests;

public sealed class BuildCommandTests : IDisposable
{
    private readonly string _root;

    public BuildCommandTests()
    {
        _root = Path.Combine(AppContext.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Cli_DispatchesKnownCommand_AndRejectsUnknownCommand()
    {
        Assert.Equal(0, await Program.Main(["--help"]));
        Assert.Equal(0, await Program.Main(["build", "--help"]));
        Assert.Equal(2, await Program.Main(["missing-command"]));
    }

    [Fact]
    public async Task Cli_PrintsInstalledVersion()
    {
        // Arrange
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.StartInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
        process.StartInfo.ArgumentList.Add("--version");

        // Act
        Assert.True(process.Start());
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = (await outputTask).Trim();

        // Assert
        Assert.True(process.ExitCode == 0, await errorTask);
        Assert.Matches(@"^dpl \d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$", output);
    }

    [Fact]
    public async Task Build_UsesBuildYamlFromCurrentDirectoryByDefault()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_root, "shared"));
        await File.WriteAllTextAsync(Path.Combine(_root, "shared", "note.md"), "note");
        await WriteConfigAsync("""
            version: 1
            steps:
              - action: copy
                source: shared/note.md
                destinations:
                  - skills/first/references
            """);
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            // Act
            Directory.SetCurrentDirectory(_root);
            var result = await Program.Main(["build"]);

            // Assert
            Assert.Equal(0, result);
            Assert.Equal("note", await File.ReadAllTextAsync(Path.Combine(_root, "skills", "first", "references", "note.md")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    [Fact]
    public async Task Build_CopiesFolder_DryRunSkipsWrites_AndChangedTargetIsReplaced()
    {
        // Arrange
        var source = Path.Combine(_root, "shared", "workflow", "nested");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(source)!, "overview.md"), "overview");
        await File.WriteAllTextAsync(Path.Combine(source, "rules.md"), "original");
        var config = await WriteConfigAsync("""
            version: 1
            steps:
              - name: workflow
                action: copy
                source: shared/workflow
                destinations:
                  - skills/first/instructions
                  - skills/second/instructions
            """);
        var first = Path.Combine(_root, "skills", "first", "instructions", "nested", "rules.md");
        var second = Path.Combine(_root, "skills", "second", "instructions", "nested", "rules.md");
        var firstRootFile = Path.Combine(_root, "skills", "first", "instructions", "overview.md");
        var secondRootFile = Path.Combine(_root, "skills", "second", "instructions", "overview.md");

        // Act and assert
        Assert.Equal(0, await Program.Main(["build", "--config", config, "--dry-run"]));
        Assert.False(File.Exists(first));
        Assert.False(File.Exists(firstRootFile));
        Assert.Equal(0, await Program.Main(["build", "--config", config]));
        Assert.Equal("original", await File.ReadAllTextAsync(first));
        Assert.Equal("original", await File.ReadAllTextAsync(second));
        Assert.Equal("overview", await File.ReadAllTextAsync(firstRootFile));
        Assert.Equal("overview", await File.ReadAllTextAsync(secondRootFile));
        Assert.False(Directory.Exists(Path.Combine(_root, "skills", "first", "instructions", "workflow")));
        await File.WriteAllTextAsync(first, "changed locally");
        Assert.Equal(0, await Program.Main(["build", "--config", config]));
        Assert.Equal("original", await File.ReadAllTextAsync(first));
    }

    [Fact]
    public async Task Build_CopiesFileWithoutDeletingOtherFiles()
    {
        // Arrange
        var source = Path.Combine(_root, "shared");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "note.md"), "note");
        var destination = Path.Combine(_root, "skills", "first", "references");
        Directory.CreateDirectory(destination);
        await File.WriteAllTextAsync(Path.Combine(destination, "other.md"), "keep");
        var config = await WriteConfigAsync("""
            version: 1
            steps:
              - action: copy
                source: shared/note.md
                destinations:
                  - skills/first/references
            """);

        // Act
        var result = await Program.Main(["build", "--config", config]);

        // Assert
        Assert.Equal(0, result);
        Assert.Equal("note", await File.ReadAllTextAsync(Path.Combine(destination, "note.md")));
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(destination, "other.md")));
    }

    [Fact]
    public async Task Build_RejectsEscapingPathBeforeAnyCopy()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_root, "shared"));
        await File.WriteAllTextAsync(Path.Combine(_root, "shared", "note.md"), "note");
        var config = await WriteConfigAsync("""
            version: 1
            steps:
              - action: copy
                source: shared/note.md
                destinations:
                  - skills/first
                  - ../outside
            """);

        // Act
        var result = await Program.Main(["build", "--config", config]);

        // Assert
        Assert.Equal(1, result);
        Assert.False(File.Exists(Path.Combine(_root, "skills", "first", "note.md")));
    }

    [Fact]
    public async Task Build_RejectsDuplicateOutputsBeforeAnyCopy()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_root, "shared"));
        await File.WriteAllTextAsync(Path.Combine(_root, "shared", "note.md"), "note");
        var config = await WriteConfigAsync("""
            version: 1
            steps:
              - action: copy
                source: shared/note.md
                destinations:
                  - skills/first
              - action: copy
                source: shared/note.md
                destinations:
                  - skills/first
            """);

        // Act
        var result = await Program.Main(["build", "--config", config]);

        // Assert
        Assert.Equal(1, result);
        Assert.False(File.Exists(Path.Combine(_root, "skills", "first", "note.md")));
    }

    private async Task<string> WriteConfigAsync(string yaml)
    {
        var path = Path.Combine(_root, "build.yaml");
        await File.WriteAllTextAsync(path, yaml);
        return path;
    }
}
