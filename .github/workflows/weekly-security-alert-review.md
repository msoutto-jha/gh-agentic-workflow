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

For each alert, capture every identifier you can:

- the **alert tag** — `Dependabot alert #<number>` or `Code scanning alert #<number>`.
  This is the primary key for everything that follows; the alert number alone is not
  unique because the two alert types number independently.
- the alert HTML URL and its API path (`dependabot/alerts/<number>` or
  `code-scanning/alerts/<number>`)
- severity
- GHSA identifier and CVE identifier (Dependabot)
- rule ID and rule name, plus file path and line (code scanning)
- affected package name, vulnerable version range, fixed version, and manifest path
- the dependency scope (direct or transitive) or the affected source file

## 2. Drop alerts that are already covered

An alert is **already covered** when an open pull request or open issue, or one closed
in the last 30 days, references it. Check in this order and stop at the first match:

1. **Title match on the alert tag (primary check).** Search issue and pull request
   *titles* for the exact alert tag captured in step 1, for example:

   ```bash
   gh search issues --repo "${{ github.repository }}" --match title "Dependabot alert #12" --state all --limit 20
   gh search prs    --repo "${{ github.repository }}" --match title "Code scanning alert #7" --state all --limit 20
   ```

   Confirm the matched title carries the same alert *type* as well as the number.

2. **Identifier match in the body (secondary check).** Only when step 1 finds nothing,
   search issue and pull request bodies and comments for the other identifiers: GHSA ID,
   CVE ID, the alert HTML URL or API path, or the rule ID together with the affected
   file path.

3. **Equivalent in-flight fix.** Check for an open Dependabot or dependency-update pull
   request that already bumps the same package past the vulnerable range.

Skip every covered alert and list it as skipped in your final summary, naming which check
matched. Do not create a duplicate pull request or issue.

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

Use `create-pull-request`. The title **must** start with the alert tag so the next run
finds it by title (the `[security] ` prefix is added automatically):

```text
Dependabot alert #12: bump Contoso.Json to 4.2.1
Code scanning alert #7: add null guard in Worker.ExecuteAsync
```

The body **must** list every identifier captured in step 1:

```markdown
## Security fix

Resolves [<alert title>](<alert HTML URL>) — **<Dependabot|Code scanning> alert #<number>**.

| Field | Value |
|---|---|
| Alert | <Dependabot\|Code scanning> alert #<number> |
| Alert URL | <alert HTML URL> |
| Severity | <severity> |
| GHSA | <GHSA ID or n/a> |
| CVE | <CVE ID or n/a> |
| Rule | <rule ID / rule name or n/a> |
| Package | <package name> |
| Vulnerable range | <range> |
| Fixed version | <version> |
| Affected path | <manifest path, or file:line> |

### Change
<one or two sentences describing exactly what changed and why it is behavior-preserving>

### Validation
<result of restore/build/test>

### Review notes
<anything a reviewer must confirm>
```

Use `n/a` for identifiers that genuinely do not apply; never omit a row and never invent
a value.

The pull request stays a draft for human review. Never merge, and never change files
outside the configured allowed paths.

## 5. Complex fix → triage issue

For each complex-fix alert, create one issue with `create-issue`. The title **must**
start with the same alert tag (the `[security triage] ` prefix is added automatically):

```text
Dependabot alert #19: Contoso.Auth 2.x requires breaking upgrade to 3.0
```

The body must contain a real triage, not a placeholder:

```markdown
## Alert

| Field | Value |
|---|---|
| Alert | <Dependabot\|Code scanning> alert #<number> |
| Alert URL | <alert HTML URL> |
| Severity | <severity> |
| GHSA | <GHSA ID or n/a> |
| CVE | <CVE ID or n/a> |
| Rule | <rule ID / rule name or n/a> |
| Package | <package name> |
| Vulnerable range | <range> |
| Fixed version | <version or none available> |
| Affected path | <manifest path, or file:line> |
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

Use `n/a` for identifiers that genuinely do not apply.

The alert tag in the title and the identifier table in the body are what make this issue
discoverable as coverage for the alert on the next run — never drop either.

## Safe outputs

- Minimal fixes: `create-pull-request` (draft, max 3 per run).
- Complex fixes: `create-issue` (max 5 per run).
- Every pull request and issue title must begin with its alert tag
  (`Dependabot alert #<number>:` or `Code scanning alert #<number>:`), and every body
  must contain the full identifier table.
- Call `noop` with a short reason when every open alert is already referenced by a
  pull request or issue, when there are no open alerts, or when alert APIs are
  unavailable for this repository.

Do not post comments, change labels on existing items, or write to the repository by
any route other than the safe outputs above.
