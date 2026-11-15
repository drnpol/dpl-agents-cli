namespace DPL.Agents.Cli.Application;

internal sealed class CopyExecutor
{
    public async Task ExecuteAsync(IReadOnlyList<CopyOperation> operations)
    {
        foreach (var operation in operations.Where(operation => !operation.IsUnchanged))
        {
            var destinationDirectory = Path.GetDirectoryName(operation.Destination)!;
            Directory.CreateDirectory(destinationDirectory);
            var temporaryPath = Path.Combine(destinationDirectory, $".dpl-{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var source = File.OpenRead(operation.Source))
                await using (var temporary = File.Create(temporaryPath))
                {
                    await source.CopyToAsync(temporary).ConfigureAwait(false);
                }

                File.Move(temporaryPath, operation.Destination, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}
