---
name: roadmap-runner
description: Advance the Clario Shop Phase 1 roadmap by one step — fix and merge the open roadmap PR if there is one, verify main is green, then implement the next unchecked step of docs/roadmap.md and open its PR. Use when a scheduled run or the user asks to continue the roadmap.
---

# Roadmap runner

Each run moves `docs/roadmap.md` forward by at most **one merge and one new step**. The owner chose:
**Claude squash-merges a roadmap PR once it is green**, and scheduled fresh sessions drive the pipeline.
Earlier steps always win: never start new work while a previous step is red, conflicted or has open review feedback.

Repository: `fahadmohammad/clario-shop`, base branch `main`. Roadmap PRs use branches named
`roadmap/<step-id>` (e.g. `roadmap/customers`) and titles starting with a conventional prefix
(`feat:`, `fix:` …). Fixes for a broken `main` use `roadmap/fix-<short-name>`.

## 0. Prepare

1. Read `CLAUDE.md`, `.claude/rules/*.md` and `docs/roadmap.md` on the latest `origin/main`.
2. Run `bash scripts/cloud-setup.sh` (local PostgreSQL + dotnet-ef), then `export PATH="$PATH:$HOME/.dotnet/tools"`.
3. Read the sections of the architecture doc that the step touches: `docs/architecture.md` is a snapshot of it.
   If the Claude Docs connector is available, prefer the live doc (link in `CLAUDE.md`; read it through the
   connector, not by fetching the URL) — where the two differ, the live doc wins.

## 1. Handle the open roadmap PR (previous step)

List open PRs whose head branch starts with `roadmap/`. There should be at most one.

**None open** → go to step 2.

**One open** — check out its branch and look at the whole PR on its current head:

| State | Action |
| --- | --- |
| Draft, last commit < 2 h old | Another run is working on it. Stop this run without changes. |
| Draft, last commit ≥ 2 h old | A previous run died mid-step. Continue that step on its branch (go to step 3, skipping branch creation). |
| Merge conflict with `main` | Merge `origin/main` into the branch (never rebase or force-push), resolve, regenerate migrations' model snapshots with `dotnet ef` if they conflict, verify, push. Stop the run. |
| CI failed | Reproduce locally (`scripts/verify.sh` or the failing project), root-cause, fix, verify, push. Stop the run. |
| CI pending | Wait for it: re-check every 1–2 minutes for up to 15 minutes, then handle the result as below. If it is still pending after 15 minutes, stop the run; the next run picks it up. |
| Unresolved review threads or change requests | Address each (fix and push, or reply with the reason if not changing), resolve threads you addressed. Stop the run. |
| CI green, mergeable, no unresolved threads, not draft | Squash-merge it (title = PR title). Continue with step 2. |

If a merge is refused (branch protection, required reviews), do not work around it: post one PR comment
saying what blocks the merge, send a push notification to the owner, and stop.

**Retry budget.** If the same PR has had 3 fix pushes in a row without CI turning green, mark its roadmap line
`[!]` (on the PR branch), add a short `> Blocked:` note under the step explaining the failure and what was tried,
comment on the PR, notify the owner, and stop. Do not start later steps while a step is blocked.

## 2. Make sure `main` is green

Check the CI result for the latest commit on `main` (the merge you just made, if any).

- Pending → wait for it: re-check every 1–2 minutes for up to 15 minutes (CI usually takes 2–3). If it is
  still pending after that, stop the run (next run continues). Do not start the next step on a pending `main`.
- Failed → this is a broken previous step. Create `roadmap/fix-<name>` from `main`, fix the root cause,
  verify, open a ready PR titled `fix: …`, and stop. The next run merges it like any roadmap PR.
- Green → step 3.

## 3. Start the next step

1. Find the first step in `docs/roadmap.md` whose box is `[ ]`. If a step is `[!]`, stop (it needs the owner).
   If every box is `[x]`: notify the owner that Phase 1 backend is complete, disable the scheduled task named
   **Clario Shop roadmap runner** (`list_triggers` → `update_trigger enabled=false`), and stop.
2. **Claim it**: branch `roadmap/<step-id>` from `origin/main`, change its box to `[~]`, commit
   `chore: start roadmap step <step-id>`, push, and open a **draft** PR titled `feat: <step title>`.
   This stops concurrent runs from starting the same step.
3. **Plan** the step briefly against the architecture doc and existing code (follow Catalog/Inventory as the
   template: Contracts project, `<Module>Module.cs`, vertical slices, `IEndpointGroup`, `ModuleDbContext`,
   migration via `dotnet ef`, permission provider, system role in `SystemRoleSynchronizer` when the step names one).
   If the step is too big for one reviewable PR, split it in `docs/roadmap.md` (e.g. `2a`, `2b`) and do only the first part.
4. **Implement with tests.** Unit tests for domain rules, integration tests for every endpoint and contract,
   architecture tests updated for any new module (`ModuleBoundaryTests` dictionaries and inline data).
   New modules: add to `ClarioShop.slnx`, the Api project references, `Program.cs` module registration, and the
   `Modules today` line in `CLAUDE.md`.
5. **Verify**: `bash scripts/verify.sh` must pass completely. For concurrency-sensitive code, check the test
   actually fails when the protection is removed. Re-read your own diff adversarially before pushing.
6. **Finish the PR**: flip the step's box to `[x]` and append ` — #<PR number>` to its title line, update
   the README status table when a module lands, push, write the PR body (what, why, tests, what is left out),
   and mark the PR **ready for review**. Stop the run; the next run merges it once CI is green.

## Never

- Skip, disable or weaken a test, or `--no-verify`, to get green.
- Force-push or rewrite history on `main` or on a roadmap branch; merge `main` in instead.
- Merge a PR with red or pending CI, unresolved review threads, or conflicts.
- Start a step while an earlier roadmap PR is open, `main` is red, or a step is blocked.
- Put secrets, model names or session details in code, commits or PR text.

## End of run

Finish with a short summary: what was merged, fixed or started (with PR links), and what the next run will do.
