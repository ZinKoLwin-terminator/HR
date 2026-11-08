---
name: hrms-security-review
description: Review HRMS changes or modules for authentication, authorization, personal-data exposure, dependency and CI security problems. Use before merging sensitive changes or for an audit.
---

# HRMS security review

## Purpose

Find real, evidenced security and privacy problems, judged against what the app actually is.

## When to use

Changes touching auth, roles, permissions, personal or pay data, file upload or import, dependencies or
CI, before connecting any real backend or data, or as a periodic audit.

## Required agents

`security-auditor`. Fixes go to the owning developer agent, followed by a re-review.

## Workflow

1. Define the scope: a branch diff, a module or the whole repository.
2. Have `security-auditor` review and report findings by severity.
3. Show the user the findings. A live secret needs immediate human rotation; removing it from code is not enough.
4. Apply approved fixes through the owning agent, then re-review them.

## Validation checklist

- [ ] No secrets or real personal data in source, fixtures, docs or the bundle
- [ ] `npm audit` reviewed, with no automatic fixes applied
- [ ] Unsafe HTML, sanitizer bypasses and import handling checked
- [ ] Workflow permissions and actions checked
- [ ] Simulated auth not presented as security
- [ ] Any backend: server-side authorization, object-level access and audit logs checked

## Expected output

Findings by severity with `file:line`, evidence and fixes; what was not reviewed; a go or no-go recommendation.
