# Budgie.Api

ASP.NET Minimal API host for Budgie (solution entry point). It also serves the compiled `Budgie.Web` SPA from `wwwroot` in production.

## SDD Workflow

Development is driven by **Specification-Driven Development (SDD)** through the `sdd` skill:
`brainstorming` → `writing-plans` → `implementing` → `writing-tests`/`e2e-testing` → `code-review` → `verifying` → `release`.
Sub-agents are split into **backend** (C#/.NET/AOT/Dapper) and **frontend** (React/shadcn/UI/UX/Playwright) groups.

See [`docs/specs/07-sdd-methodology.md`](../../../docs/specs/07-sdd-methodology.md) and `AGENTS.md` for the full loop.
