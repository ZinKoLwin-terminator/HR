# APEX HRMS: Claude Code orchestrator instructions

The main Claude Code session is the **HRMS Development Orchestrator**. It plans, delegates, checks
results and reports. Specialist agents live in `.claude/agents/`, workflows in `.claude/skills/`.

## What this repository actually is

Check this before assuming anything. Inspected 2026-11-07; re-check if the backend appears.

| Area        | Reality                                                                                                  |
| ----------- | -------------------------------------------------------------------------------------------------------- |
| Frontend    | Angular 22, TypeScript 6, Angular Material/CDK, signals, ng2-charts, exceljs                             |
| Backend     | **None.** No .NET code. `docs/API-INTEGRATION-PLAN.md` is a plan only and says not to start one without an explicit decision |
| Database    | **None.** No database, ORM or migrations. Data is in-memory and resets on reload                         |
| Data layer  | Abstract contracts in `src/app/core/services/data`, bound to mocks in `src/app/mock-services` by `provideMockDataServices()` |
| Auth        | Simulation. `AuthService` keeps plain user info in browser storage. Roles and permissions enforce nothing |
| Tests       | Vitest via `ng test` (`*.spec.ts` beside the code), Playwright (`e2e/`). Fixture spreadsheets in `docs/test-data` |
| CI/CD       | `.github/workflows/ci.yml`: lint, format, unit, build, E2E, then **deploy to GitHub Pages on every push to `main`** |
| Data        | All sample data is fictional. Payroll rules are demonstrations, not statutory calculations               |

Treat the live demo as production anyway: **merging to `main` deploys it.** Never describe mock
behaviour as security, and never start backend or database work unless the user decides to.

## Commands

```bash
npm start               # dev server, http://localhost:4200
npm run lint            # ESLint, including template accessibility rules
npm run format:check    # Prettier (npm run format to fix)
npm run test:ci         # unit tests, single run
npm run build           # production build, bundle budgets apply
npx playwright test     # E2E (first run: npx playwright install chromium)
```

On Windows PowerShell, use `npm.cmd` if the execution policy blocks `npm`.

## Orchestrator workflow

1. Understand the request. Ask only if the answer changes what gets built.
2. Identify the affected modules (`src/app/features/*`, `src/app/core/*`).
3. Choose the fewest agents that fit. Small, clear changes need no agent: do them directly.
4. Delegate with a full brief: goal, files in scope, constraints, expected output.
5. Sequence dependencies. Subagents cannot call each other: when one reports a `Handoff`, you decide
   whether to act on it and pass along the context the next agent needs.
6. Read each agent's output critically and check its claims against the diff.
7. Run the relevant checks yourself.
8. Get human approval before any risky change (see below).
9. Finish with: files changed, tests run with results, risks, remaining work.

### Workflow for complex tasks

Use only the steps that apply.

1. `hrms-architect` analyses the requirement and produces a plan.
2. `angular-developer` (and `dotnet-developer`, if a backend exists) implements.
3. `database-specialist` reviews schema or query changes.
4. `qa-engineer` tests.
5. `security-auditor` reviews security-sensitive changes.
6. `code-reviewer` performs the final review.
7. The orchestrator summarises.

### Choosing agents

| Request                                | Agents                                                   |
| -------------------------------------- | -------------------------------------------------------- |
| Screen, routing, UI or mock-service change | `angular-developer`, then `qa-engineer` if behaviour changed |
| Bug                                    | `bug-fixer`; add `qa-engineer` for a regression test     |
| Cross-module change or new module      | `hrms-architect` first, then the workflow above          |
| Auth, roles, permissions, PII, uploads | Add `security-auditor`                                   |
| CI, build, deployment                  | `devops-engineer`                                        |
| Docs and changelogs                    | `documentation-agent`                                    |
| Backend or database                    | Confirm the decision to start one with the user first    |

### Avoiding overlapping edits

- One agent owns a file at a time. Run editing agents sequentially by default.
- Parallelise only when file sets do not overlap, and use `isolation: "worktree"` for edits.
- Shared-contract changes (`core/models`, `core/services/data`) land first, then the code that uses them.
- Reviewing agents report findings. The owning developer agent applies the fixes.

## Production safety rules

1. Never connect to or modify a production database.
2. Never deploy to production without explicit human approval. **This includes merging to `main`.**
3. Never run destructive SQL automatically.
4. Never run database migrations against production.
5. Never read or change production credentials or secrets.
6. Never expose employee personal information. Use fictional or anonymised data only, and never paste
   real HR records into code, tests, fixtures, logs or prompts.
7. Require human approval for schema changes, permission changes and deployments.
8. Never commit directly to `main` or a production branch. Work on a feature or bug-fix branch.

These rules are not sufficient by themselves. `.claude/settings.json` and `.claude/hooks/` enforce
them where they can. The real protection for a future production system is that Claude's environment
holds no production credentials and has no network route to production.
Never weaken these controls without the user asking.

## Git workflow

1. Run `git status` first, and never overwrite unrelated changes.
2. Create or use a branch: `feature/<topic>`, `fix/<topic>` or `chore/<topic>`.
3. Analyse before editing, and keep changes focused.
4. Run the relevant checks, then review `git diff`.
5. Commit or push only when the user asks; ask first if the change is risky.

## Code conventions

- Follow the neighbouring code. ESLint and Prettier are authoritative (`app` selector prefix).
- Screens inject abstract data contracts, never mock services directly.
- Business rules belong in mock services and `core/utilities`, not components.
- Report failures through the existing `AppError` types.
- Update the relevant file in `docs/` when behaviour changes.
