---
emoji: 🛡️
description: |
  Weekly review of open GitHub security alerts (Dependabot and code scanning).
  Opens a draft pull request for minimal, low-risk fixes and a triage issue with
  remediation options when the fix is complex or would change behavior.
intent: |
  Every open security alert that is not already covered by an existing pull request
  or issue receives either a minimal draft fix pull request or a triage issue with
  suggested remediation options for a human to pick up.

on:
  schedule: weekly

engine: copilot

permissions:
  contents: read
  issues: read
  pull-requests: read
  security-events: read
  vulnerability-alerts: read

tools:
  github:
    mode: gh-proxy
    toolsets: [default, code_security, dependabot, security_advisories]

network:
  allowed:
    - defaults
    - dotnet

safe-outputs:
  create-pull-request:
    title-prefix: "[security] "
    labels: [security, automated]
    draft: true
    max: 3
    allowed-files:
      - "src/**/*.cs"
      - "src/**/*.csproj"
      - "src/**/*.props"
      - "src/**/*.targets"
      - "Directory.Build.props"
      - "Directory.Packages.props"
      - "NuGet.config"
      - "nuget.config"
  create-issue:
    title-prefix: "[security triage] "
    labels: [security, triage]
    max: 5

timeout-minutes: 20
---

# Weekly Security Alert Review

Review the open security alerts in `${{ github.repository }}` and make sure each one
that nobody is working on yet is either fixed by a minimal draft pull request or
written up as a triage issue for a human.

Handle **at most 5 alerts** in this run. Prefer the highest severity alerts first,
then the oldest.

## 1. Collect open alerts

Use `gh` to read alerts. Request only the fields you need and page explicitly.

```bash
gh api -X GET "/repos/${{ github.repository }}/dependabot/alerts" -f state=open -F per_page=100
gh api -X GET "/repos/${{ github.repository }}/code-scanning/alerts" -f state=open -F per_page=100
```

If an endpoint returns 403 or 404, the feature is disabled or not enabled for this
repository. Record that and continue with the other alert source; do not fail the run.

For each alert, capture a stable identity: alert number, alert HTML URL, severity,
the GHSA/CVE identifier (Dependabot) or rule ID plus file path and line (code scanning),
and the affected package, manifest, or source file.

## 2. Drop alerts that are already covered

An alert is **already covered** when an open pull request or an open issue, or an item
closed in the last 30 days, references it. Check with targeted searches before acting:

- search for the GHSA or CVE identifier
- search for the alert HTML URL or `dependabot/alerts/<number>` / `code-scanning/alerts/<number>`
- search for the rule ID together with the affected file path
- check for an open Dependabot pull request that bumps the same package

Skip every covered alert and say so in your final summary. Do not create a duplicate
pull request or issue.

## 3. Classify each remaining alert

Decide between a **minimal fix** and a **complex fix** using the evidence in the
repository, not assumptions.

Treat the fix as **minimal** when all of these hold:

- it is a patch or minor dependency version bump with no breaking API change, or a small
  localized code change (added guard, input validation, safe API swap, corrected escaping)
- it does not change business logic, public API shape, persisted data, or observable behavior
- it is confined to the files allowed for pull requests in this workflow
- you can confirm the fixed version exists and the affected package is a direct dependency
  you can edit in a project file

Treat the fix as **complex** when any of these hold:

- it requires a major version upgrade or a breaking API migration
- it changes business logic, worker lifecycle behavior, authentication/authorization
  semantics, configuration contracts, or output format
- no fixed version is available, or the remediation requires a design decision
- the fix spans multiple components, requires new or rewritten tests, or touches files
  outside the allowed pull request paths
- you cannot establish with confidence that the change is safe

When in doubt, classify as **complex**. A wrong issue is cheap; a wrong pull request is not.

## 4. Minimal fix → draft pull request

For each minimal-fix alert, make the smallest change that resolves it, one alert per
pull request.

Validate before proposing the change:

```bash
dotnet restore ./src/gh-agentic-workflow.slnx
dotnet build ./src/gh-agentic-workflow.slnx
dotnet test ./src/gh-agentic-workflow.slnx
```

If the build or tests fail and you cannot fix it with the same minimal change, abandon
the pull request and create a triage issue for that alert instead.

Use `create-pull-request` with a body that references the alert explicitly:

```markdown
## Security fix

Resolves [<alert title>](<alert HTML URL>) (<severity>, <GHSA/CVE or rule ID>).

| Field | Value |
|---|---|
| Alert | <alert HTML URL> |
| Severity | <severity> |
| Identifier | <GHSA/CVE or rule ID> |
| Affected | <package and version range, or file:line> |

### Change
<one or two sentences describing exactly what changed and why it is behavior-preserving>

### Validation
<result of restore/build/test>

### Review notes
<anything a reviewer must confirm>
```

The pull request stays a draft for human review. Never merge, and never change files
outside the configured allowed paths.

## 5. Complex fix → triage issue

For each complex-fix alert, create one issue with `create-issue` containing a real
triage, not a placeholder:

```markdown
## Alert

| Field | Value |
|---|---|
| Alert | <alert HTML URL> |
| Severity | <severity> |
| Identifier | <GHSA/CVE or rule ID> |
| Affected | <package and version range, or file:line> |
| Introduced by | <direct or transitive dependency, or code path> |

## Impact in this repository
<how the vulnerable code or package is actually reached here, or why exposure is limited>

## Why this is not a minimal fix
<the specific breaking change, behavior change, or missing fix version>

## Suggested options

### Option 1 — <name>
- Effort: <low/medium/high>
- Change: <what to do>
- Risk / behavior change: <what could break>

### Option 2 — <name>
- Effort: <low/medium/high>
- Change: <what to do>
- Risk / behavior change: <what could break>

### Option 3 — Accept and monitor
- Justification required: <conditions under which this is reasonable>

## Recommendation
<one option, with the reason>
```

Include the alert URL so the issue is discoverable as coverage for that alert on the
next run.

## Safe outputs

- Minimal fixes: `create-pull-request` (draft, max 3 per run).
- Complex fixes: `create-issue` (max 5 per run).
- Call `noop` with a short reason when every open alert is already referenced by a
  pull request or issue, when there are no open alerts, or when alert APIs are
  unavailable for this repository.

Do not post comments, change labels on existing items, or write to the repository by
any route other than the safe outputs above.
