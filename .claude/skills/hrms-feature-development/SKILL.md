---
name: hrms-feature-development
description: Plan, implement, test and review a new HRMS feature or significant behaviour change across the agent team. Use for new screens, workflows or module changes.
---

# HRMS feature development

## Purpose

Deliver a feature that fits the architecture, using only the agents it needs.

## When to use

New screens, workflows, fields or behaviour changes. A one-file tweak does not need this workflow.

## Required agents

`angular-developer` always. `hrms-architect` for cross-module or contract changes. `qa-engineer` for tests,
`security-auditor` for auth, permissions, personal data or uploads, `code-reviewer` for non-trivial
changes, and `documentation-agent` for behaviour docs. Use `dotnet-developer` and `database-specialist`
only after the user has decided to start a backend.

## Workflow

1. Run `git status`, then create `feature/<topic>`. Read `docs/FEATURES.md` for the module.
2. Plan: use `hrms-architect` if the change crosses modules. Get approval for risky decisions.
3. Implement in order: models and contracts, then mock services, then screens, navigation and guides.
4. Test with `qa-engineer`.
5. Run reviews where relevant. Send findings back to the owning developer agent.
6. Update docs, run all checks, review the diff, and summarise.

## Validation checklist

- [ ] Screens use abstract contracts, and rules sit outside components
- [ ] New behaviours have tests, including failure paths
- [ ] `npm run lint`, `npm run format:check`, `npm run test:ci` and `npm run build` pass
- [ ] Relevant `npx playwright test` specs pass
- [ ] Docs updated, and sample data is fictional
- [ ] Nothing simulated is presented as real

## Expected output

Summary, files changed, test results, review findings and how they were resolved, risks, remaining work.
