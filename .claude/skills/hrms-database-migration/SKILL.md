---
name: hrms-database-migration
description: Design and review a database schema change or migration for the HRMS. No database exists yet. Prepares migrations for a human to apply; never runs them.
disable-model-invocation: true
---

# HRMS database migration

## Purpose

Prepare safe, reviewed schema changes that a human applies.

## When to use

Only when a database exists, or the user has decided to design one. **Today this repository has none.**
Confirm that with a search first, and stop if the decision has not been made.

## Required agents

`database-specialist`, `hrms-architect` for new data models, `dotnet-developer` for application changes,
`security-auditor` for sensitive columns, and `qa-engineer` for migration tests.

## Workflow

1. Detect the actual engine, ORM and migration tool from the repository. Never assume.
2. Create a branch and have `database-specialist` design the change.
3. Review for data loss, locking, reversibility, personal data and audit history.
4. Present the migration and rollback plan to the user, and get approval for the schema change.
5. **A human** applies it to a local or non-production database containing anonymised data, and shares the result.
6. **A human** schedules any production rollout, after a verified backup.

## Validation checklist

- [ ] Engine and tooling detected from the repository
- [ ] Destructive statements called out at the top
- [ ] Rollback plan written and backup step stated
- [ ] Sensitive columns classified, with access reviewed
- [ ] No connection strings or credentials in any file
- [ ] Human approval recorded before anything is applied
- [ ] Claude ran no migration against any database

## Expected output

Migration files, risk assessment, rollback plan, and exact steps for a human to run.
