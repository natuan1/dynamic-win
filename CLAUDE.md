## Agent skills

### Issue tracker

Issues and specs live in GitHub Issues; use the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Domain docs

This is a single-context repo using root `CONTEXT.md` and `docs/adr/`. See `docs/agents/domain.md`.

## Working rules

- Keep responses and changes token-efficient; be concise and actionable.
- Apply Clean Architecture, SOLID, KISS, YAGNI, DRY, and separation of concerns.
- Use a dedicated branch for every feature or bug fix. When complete, verify the change, then open a pull request targeting `main`.
- Repository migration: before the first PR workflow, get explicit confirmation, remove the existing `.git` metadata, create the GitHub repository `dynamic-win`, initialize the new remote, and push all code. Never delete `.git` without that confirmation.
