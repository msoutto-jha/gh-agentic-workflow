# gh-agentic-workflow

A small .NET playground for experimenting with GitHub agentic workflows. The repository currently contains a .NET worker service and an MSTest project that exercises the worker; it is intended as a starting point for workflow experiments, not a production application.

## Projects

- `src/WorkerTemplate` — .NET 10 hosted worker that logs a message once per second.
- `src/WorkerTemplate.Tests` — MSTest unit tests for the worker.
- `src/SecurityAlertFixtures` — non-packable, dependency-only project for exercising Dependabot alert review.
- `src/gh-agentic-workflow.slnx` — solution containing all three projects.

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

## Dependabot exercise

`SecurityAlertFixtures` intentionally references `Microsoft.Data.SqlClient` 5.1.2,
which is affected by [GHSA-98g6-xh36-x2p7](https://github.com/advisories/GHSA-98g6-xh36-x2p7).
The first patched version for this release line is 5.1.3. The project contains no
application code, is not referenced by the worker or its tests, and must not be
used in deployments.

With the dependency graph and Dependabot alerts enabled, this reference on the
default branch is intended to exercise `.github/workflows/weekly-security-alert-review.md`.
Keep automatic Dependabot security updates disabled during the exercise if the
agentic workflow should create the fix PR itself; it skips alerts already covered
by another PR. Restore reports vulnerability warnings for this intentional fixture,
including its transitive `Azure.Identity`, `Microsoft.IdentityModel.JsonWebTokens`,
and `System.IdentityModel.Tokens.Jwt` dependencies. The exercise may therefore
produce multiple alerts, including transitive-dependency triage rather than just
the direct SqlClient patch-fix PR.

Update or remove the vulnerable reference after the exercise.
