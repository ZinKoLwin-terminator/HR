---
name: hrms-testing
description: Run and extend unit, regression and Playwright end-to-end tests for HRMS modules. Use to verify a change, add coverage or classify test failures.
---

# HRMS testing

## Purpose

Establish with evidence whether a change or module works, and close coverage gaps.

## When to use

After changes, before a merge, when adding coverage, or when tests fail.

## Required agents

`qa-engineer`. Send product bugs to `bug-fixer`, and pipeline-only failures to `devops-engineer`.

## Workflow

1. Identify the behaviours at risk from `git diff` and `docs/TEST-SCENARIOS.md`.
2. Run what exists: `npm run test:ci`, then `npx playwright test` or `ui/e2e/<module>.spec.ts`.
3. Classify each failure as product bug, test bug or environment problem.
4. Add tests for uncovered behaviours and boundaries, using the known gaps in `docs/TESTING.md` as a guide.
5. Re-run and report exact counts.

## Validation checklist

- [ ] Commands and exact results reported
- [ ] No test skipped, weakened or deleted to get a pass
- [ ] Failure paths and boundaries covered, not only the happy path
- [ ] Only fictional or anonymised test data used
- [ ] `npm run lint` and `npm run format:check` pass on new test files

## Expected output

Tests added, commands run, pass and fail counts, classified failures, remaining gaps.
