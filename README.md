# dpl

`dpl` is a .NET 10 command-line tool that builds agent resources from a YAML file.

For a folder source, `copy` places its contents directly in each destination and keeps the layout of nested folders. For example, copying `instructions/development-workflow` to `skills/create-development-work/instructions` produces `skills/create-development-work/instructions/document-conventions.md`. A single-file source keeps its filename.

## Build and install locally

```powershell
dotnet pack src/DPL.Agents.Cli/DPL.Agents.Cli.csproj -c Release -o artifacts/package
dotnet tool install --global --add-source artifacts/package DPL.Agents.Cli
```
