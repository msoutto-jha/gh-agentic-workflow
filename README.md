# gh-agentic-workflow

A small .NET playground for experimenting with GitHub agentic workflows. The repository currently contains a .NET worker service and an MSTest project that exercises the worker; it is intended as a starting point for workflow experiments, not a production application.

## Projects

- `src/WorkerTemplate` — .NET 10 hosted worker that logs a message once per second.
- `src/WorkerTemplate.Tests` — MSTest unit tests for the worker.
- `src/gh-agentic-workflow.slnx` — solution containing both projects.

## Requirements

- .NET 10 SDK

## Run locally

From the repository root, restore dependencies and run the tests:

```powershell
dotnet restore .\src\gh-agentic-workflow.slnx
dotnet test .\src\gh-agentic-workflow.slnx
```

Start the worker with:

```powershell
dotnet run --project .\src\WorkerTemplate\WorkerTemplate.csproj
```

The worker writes a log message every second. Stop it with `Ctrl+C`.
