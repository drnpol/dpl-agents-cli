namespace DPL.Agents.Cli.Application;

internal sealed class CopyPlanner
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public async Task<IReadOnlyList<CopyOperation>> PlanAsync(LoadedConfiguration loaded)
    {
        var operations = new List<CopyOperation>();
        var outputs = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (var (step, index) in loaded.Configuration.Steps!.Select((step, index) => (step, index)))
        {
            var label = string.IsNullOrWhiteSpace(step.Name) ? $"step {index + 1}" : step.Name;
            if (step.Action != "copy")
            {
                throw new BuildException($"{label}: unsupported action '{step.Action}'. Expected 'copy'.");
            }

            if (string.IsNullOrWhiteSpace(step.Source) || step.Destinations is not { Count: > 0 })
            {
                throw new BuildException($"{label}: source and at least one destination are required.");
            }

            var source = ResolvePath(loaded.RootDirectory, step.Source, label);
            RejectReparsePoints(loaded.RootDirectory, source, label);
            var sourceIsFile = File.Exists(source);
            var sourceIsDirectory = Directory.Exists(source);
            if (!sourceIsFile && !sourceIsDirectory)
            {
                throw new BuildException($"{label}: source not found: {source}");
            }

            foreach (var destination in step.Destinations)
            {
                var destinationDirectory = ResolvePath(loaded.RootDirectory, destination, label);
                RejectReparsePoints(loaded.RootDirectory, destinationDirectory, label);
                if (File.Exists(destinationDirectory))
                {
                    throw new BuildException($"{label}: destination must be a directory: {destinationDirectory}");
                }

                if (sourceIsDirectory && IsInside(source, destinationDirectory))
                {
                    throw new BuildException($"{label}: destination cannot be inside its source folder.");
                }

                var targetRoot = Path.Combine(destinationDirectory, Path.GetFileName(source));
                var sourceFiles = sourceIsFile ? [source] : EnumerateSourceFiles(source, label);
                foreach (var sourceFile in sourceFiles)
                {
                    var target = sourceIsFile
                        ? targetRoot
                        : Path.Combine(targetRoot, Path.GetRelativePath(source, sourceFile));
                    RejectReparsePoints(loaded.RootDirectory, target, label);
                    if (Directory.Exists(target))
                    {
                        throw new BuildException($"{label}: output path is a directory: {target}");
                    }

                    if (string.Equals(sourceFile, target, PathComparison))
                    {
                        throw new BuildException($"{label}: source and output are the same file: {target}");
                    }

                    if (!outputs.Add(target))
                    {
                        throw new BuildException($"Multiple rules produce the same output: {target}");
                    }

                    operations.Add(new CopyOperation(
                        sourceFile,
                        target,
                        await FilesMatchAsync(sourceFile, target).ConfigureAwait(false)));
                }
            }
        }

        return operations;
    }

    private static string ResolvePath(string root, string? relativePath, string label)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new BuildException($"{label}: paths must be non-empty and relative to the config file.");
        }

        var absolute = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!IsInside(root, absolute))
        {
            throw new BuildException($"{label}: path escapes the config directory: {relativePath}");
        }

        return absolute;
    }

    private static bool IsInside(string directory, string path)
    {
        var relative = Path.GetRelativePath(directory, path);
        return relative == "." || (!Path.IsPathRooted(relative)
            && relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", PathComparison));
    }

    private static void RejectReparsePoints(string root, string path, string label)
    {
        var current = root;
        foreach (var part in Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            current = Path.Combine(current, part);
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new BuildException($"{label}: symbolic links and junctions are not supported: {current}");
            }
        }
    }

    private static List<string> EnumerateSourceFiles(string source, string label)
    {
        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(source);
        while (pending.Count > 0)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(pending.Pop()).Order(StringComparer.Ordinal))
            {
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new BuildException($"{label}: symbolic links and junctions are not supported: {entry}");
                }

                if (Directory.Exists(entry)) pending.Push(entry);
                else files.Add(entry);
            }
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    private static async Task<bool> FilesMatchAsync(string source, string target)
    {
        if (!File.Exists(target) || new FileInfo(source).Length != new FileInfo(target).Length)
        {
            return false;
        }

        await using var sourceStream = File.OpenRead(source);
        await using var targetStream = File.OpenRead(target);
        var sourceBuffer = new byte[81920];
        var targetBuffer = new byte[81920];
        int read;
        while ((read = await sourceStream.ReadAsync(sourceBuffer).ConfigureAwait(false)) > 0)
        {
            await targetStream.ReadExactlyAsync(targetBuffer.AsMemory(0, read)).ConfigureAwait(false);
            if (!sourceBuffer.AsSpan(0, read).SequenceEqual(targetBuffer.AsSpan(0, read))) return false;
        }

        return true;
    }
}
