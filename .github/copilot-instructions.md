# Repository instructions

## Build, test, and run

The solution and both projects target .NET 10. From the repository root:

```powershell
dotnet restore .\src\gh-agentic-workflow.slnx
dotnet build .\src\gh-agentic-workflow.slnx
dotnet test .\src\gh-agentic-workflow.slnx
dotnet run --project .\src\WorkerTemplate\WorkerTemplate.csproj
```

Run the worker test by name:

```powershell
dotnet test .\src\WorkerTemplate.Tests\WorkerTemplate.Tests.csproj --filter "FullyQualifiedName~StartAsync_LogsWorkerRunning_AndStopAsyncCompletes"
```

There is no separate repository lint command configured.

## Architecture

- `src/WorkerTemplate` is the .NET Generic Host worker executable. `Program.cs` registers `Worker` as a hosted service; `Worker.cs` implements the cancellable background loop and logs through `ILogger<Worker>`.
- `src/WorkerTemplate.Tests` references the worker project and contains MSTest coverage for hosted-service behavior. The solution at `src/gh-agentic-workflow.slnx` includes both projects.
- `.github/workflows/*.md` contains Agentic Workflow source; matching `.lock.yml` files are generated workflow artifacts. The repository also has an Agentic Workflows agent/skill and configures its MCP server in `.github/mcp.json`.

## Conventions

- Keep worker execution in `BackgroundService.ExecuteAsync`, honor its `stoppingToken`, and use the injected typed logger rather than writing directly to the console.
- Worker tests exercise the hosted-service lifecycle with `StartAsync`/`StopAsync`; use a recording `ILogger<T>` and a bounded cancellation timeout when waiting for asynchronous log output.
- When changing an Agentic Workflow, edit its Markdown source and regenerate/validate the lock file with `gh aw compile --validate`. Use `gh aw run <workflow-name>` to trigger one; do not trigger its generated lock file with `gh workflow run`.
- For Agentic Workflow-specific design, edits, or debugging, follow `.github/agents/agentic-workflows.md` and the relevant `.github/skills/agentic-workflows/SKILL.md` guidance.
