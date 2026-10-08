---
emoji: 🔍
description: |
  Daily review of open pull requests that have no review yet. Reviews at most 5 PRs
  per run, focusing on inconsistencies, unused code, and unreachable code, and tags
  each reviewed PR with the `ai-reviewed` label so later runs can skip it.
intent: |
  Every open, non-draft pull request that has not been reviewed by anyone receives one
  COMMENT or REQUEST_CHANGES review that flags inconsistencies, unused code, and
  unreachable code, and is labeled `ai-reviewed` so it is never reviewed twice.

on:
  schedule: daily on weekdays
  workflow_dispatch:

permissions:
  contents: read
  pull-requests: read
  issues: read

tools:
  github:
    mode: gh-proxy
    toolsets: [default]

network:
  allowed:
    - defaults
    - dotnet

safe-outputs:
  create-pull-request-review-comment:
    max: 50
    side: "RIGHT"
    target: "*"
  submit-pull-request-review:
    max: 5
    target: "*"
    allowed-events: [COMMENT, REQUEST_CHANGES]
  add-labels:
    allowed: [ai-reviewed]
    max: 5
    target: "*"

timeout-minutes: 30
---

# Daily Pull Request Review

Review open pull requests in `${{ github.repository }}` that nobody has reviewed yet.
Review **at most 5 pull requests** in this run.

## Review identifier

Every pull request this workflow reviews is marked in two ways:

1. The **`ai-reviewed` label** — the primary identifier. Query unreviewed PRs with
   `-label:ai-reviewed`, and humans can filter with `label:ai-reviewed`.
2. A hidden marker as the first line of the review body, containing the reviewed head
   commit SHA:

   ```text
   <!-- daily-pr-review: sha=<head commit SHA> -->
   ```

Always add both. Never review a PR that already carries either identifier.

## 1. Find candidate pull requests

List open, non-draft PRs without the label, oldest first:

```bash
gh pr list --repo "${{ github.repository }}" --state open --draft=false \
  --search "-label:ai-reviewed sort:created-asc" --limit 50 \
  --json number,title,author,headRefOid,reviewDecision,createdAt
```

For each candidate, check whether it already has a review:

```bash
gh api "/repos/${{ github.repository }}/pulls/<number>/reviews" --jq 'map({user: .user.login, state, body})'
```

Skip the PR when **any** submitted review exists (from a human or a bot), or when any
review body contains `<!-- daily-pr-review:`. Also skip PRs authored by bots
(for example `dependabot[bot]`). Pending reviews do not count.

Take the first 5 remaining PRs. If none remain, call `noop` with a short reason
(for example "All 7 open PRs already have a review") and stop.

## 2. Review each selected pull request

For each selected PR, read the diff and the surrounding code:

```bash
gh pr diff <number> --repo "${{ github.repository }}"
gh pr view <number> --repo "${{ github.repository }}" --json title,body,files,headRefOid,baseRefName
```

Check out the PR head when you need full-file context:

```bash
git fetch origin "pull/<number>/head:pr-<number>" && git checkout "pr-<number>"
```

Look for these problems, and only report findings you can support with evidence from
the code:

- **Unused code** — new or modified methods, classes, fields, parameters, local
  variables, `using` directives, configuration keys, or DI registrations that nothing
  references. Search the whole repository (for example `grep -rn "<Symbol>" src/`)
  before reporting, and account for reflection, DI, and test usage.
- **Unreachable / never-hit code** — branches whose conditions can never be true,
  code after unconditional `return`/`throw`/`break`, `catch` blocks for exceptions the
  `try` cannot raise, cases that duplicate earlier ones, and paths that tests and
  callers can never exercise.
- **Inconsistencies** — behavior that contradicts the PR title or description, naming
  or patterns that diverge from the surrounding code, mismatched logging/error
  handling, configuration that disagrees with code, tests that do not assert what
  their names claim, and changes that ignore repository conventions (for example
  `BackgroundService.ExecuteAsync` not honoring `stoppingToken`, or writing to the
  console instead of the injected `ILogger<T>`).

Do not comment on pure style or formatting. Prefer a few high-confidence findings over
many speculative ones.

If it helps confirm a finding, you may build and test the PR:

```bash
dotnet build ./src/gh-agentic-workflow.slnx
dotnet test ./src/gh-agentic-workflow.slnx
```

## 3. Publish the review

For each reviewed PR:

1. For each finding tied to a specific changed line, call
   `create_pull_request_review_comment` with the PR number, the file path, and the line
   on the RIGHT side of the diff. Explain the problem and suggest a concrete fix.
2. Call `submit_pull_request_review` for that PR number exactly once:
   - Use `REQUEST_CHANGES` only for clear bugs or unreachable/unused code that should
     not merge. Otherwise use `COMMENT`. Never approve.
   - The body must start with the hidden marker
     `<!-- daily-pr-review: sha=<head commit SHA> -->`, followed by a short summary:

     ```markdown
     <!-- daily-pr-review: sha=<head commit SHA> -->
     ## Automated review

     **Findings:** <n> (unused code: <n>, unreachable code: <n>, inconsistencies: <n>)

     <one bullet per finding, including findings not tied to a single line>
     ```

   - When you find nothing, still submit a `COMMENT` review that says no unused code,
     unreachable code, or inconsistencies were found, so the PR is marked reviewed.
3. Call `add_labels` with `ai-reviewed` for that PR number.

## Safe outputs

- `create_pull_request_review_comment` — inline findings.
- `submit_pull_request_review` — one review per PR, `COMMENT` or `REQUEST_CHANGES`.
- `add_labels` — add `ai-reviewed` to every PR you reviewed.
- `noop` — when there are no unreviewed PRs to review.

Do not push commits, edit PR descriptions, or write to the repository by any other route.
