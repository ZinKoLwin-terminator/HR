---
name: hrms-bug-fix
description: Investigate and fix an HRMS bug or failing test with root-cause analysis and a regression test. Use when something is broken, wrong or failing.
---

# HRMS bug fix

## Purpose

Fix the cause of a bug with the smallest safe change, and prevent it from coming back.

## When to use

A reported defect, wrong calculation, failing test or CI failure caused by product code.
Not for new behaviour (use `hrms-feature-development`). Remember that data resetting on reload is by design.

## Required agents

`bug-fixer`. Add `qa-engineer` for wider regression coverage, `security-auditor` if data could be exposed,
and `hrms-architect` if the cause is a cross-module design flaw.

## Workflow

1. Run `git status`, then create `fix/<topic>` from an up-to-date `main`.
2. Have `bug-fixer` reproduce the bug and find the root cause.
3. Apply the minimal fix and a regression test that fails without it.
4. Run the checks below yourself and review the diff.
5. Commit when the user asks.

## Validation checklist

- [ ] Bug reproduced, or reproduction failure reported
- [ ] Root cause identified with `file:line`
- [ ] Regression test added, or the reason it was impractical stated
- [ ] `npm run lint`, `npm run format:check` and `npm run test:ci` pass
- [ ] Matching `npx playwright test e2e/<module>.spec.ts` passes for user-visible flows
- [ ] Diff contains no unrelated changes

## Expected output

Root cause, fix, files changed, test results, and remaining risks.
