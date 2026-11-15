# dpl

`dpl` is a .NET 10 command-line tool that builds agent resources from a YAML file.

## Build and install locally

```powershell
dotnet pack src/DPL.Agents.Cli/DPL.Agents.Cli.csproj -c Release -o artifacts/package
dotnet tool install --global --add-source artifacts/package DPL.Agents.Cli
```
