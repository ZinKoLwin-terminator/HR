---
name: hrms-production-deployment
description: Prepare and verify an HRMS release for a human to deploy. Merging to main deploys to GitHub Pages. Claude prepares and checks only; it never merges or deploys.
disable-model-invocation: true
---

# HRMS production deployment

## Purpose

Get a release ready and give a human what they need to decide and deploy.

## When to use

When the user asks to prepare a release. Run it only when they invoke it.

## How deployment works here

`.github/workflows/ci.yml` deploys to GitHub Pages on every push to `main` that passes verification and
E2E. **Merging the PR is the deployment.** There is no separate approval step unless one is configured
in the repository's settings.

## Required agents

`qa-engineer` for the release checks, `code-reviewer` for the final review, `security-auditor` for sensitive
changes, `devops-engineer` for pipeline changes, and `documentation-agent` for release notes.

## Workflow

1. Confirm the branch is not `main` and the working tree is clean, then list what is shipping with `git log main..HEAD`.
2. Run all the checks below locally.
3. Run reviews appropriate to the risk.
4. Prepare release notes, and a rollback note: revert the merge commit on `main`, which redeploys the previous version.
5. Push the branch and open a PR only if the user approves.
6. **Stop.** A human reviews CI and merges.
7. After the merge, ask the human to check the live site: sign-in, dashboard and the changed screens.

## Validation checklist

- [ ] `npm run lint`, `npm run format:check`, `npm run test:ci` and `npm run build` pass
- [ ] `npx playwright test` passes
- [ ] No secrets or real personal data in the diff
- [ ] Reviews complete, with blocking findings resolved
- [ ] Rollback note written
- [ ] Explicit human approval given for the merge
- [ ] Claude did not merge, deploy, or trigger a workflow

## Expected output

Release summary, check results, known risks, rollback steps, and the action the human needs to take.
