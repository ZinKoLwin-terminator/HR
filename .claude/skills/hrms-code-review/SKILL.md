---
name: hrms-code-review
description: Review an HRMS branch or diff for correctness, architecture compliance, performance and maintainability. Use before merging non-trivial changes.
---

# HRMS code review

## Purpose

Catch real problems before merge, without noise.

## When to use

Non-trivial changes before a merge, or when the user asks for a review.

## Required agents

`code-reviewer`. Add `security-auditor` for sensitive areas. The owning developer agent applies fixes.

## Workflow

1. Identify the diff: `git diff main...HEAD` and any uncommitted changes.
2. Tell `code-reviewer` what the change is meant to do.
3. Check each reported finding against the code before accepting it.
4. Send accepted blocking findings to the owning agent, then re-review those fixes.
5. Run the checks and report.

## Validation checklist

- [ ] Correctness and failure paths reviewed
- [ ] Screens use abstract contracts, with rules outside components
- [ ] Tests and docs match the change
- [ ] `npm run lint`, `npm run format:check`, `npm run test:ci` and `npm run build` pass
- [ ] Blocking findings fixed or explicitly accepted by the user

## Expected output

Findings by severity with `file:line`, a verdict (approve, approve with changes, or changes required), and check results.
