# Budgie.Api

ASP.NET Minimal API host for Budgie (solution entry point). It also serves the compiled `Budgie.Web` SPA from `wwwroot` in production.

## Harness

The project ships a headless harness at `harness/run.sh` (repo root) that dispatches a task to `opencode run` and verifies the result with real .NET/Bun gates before reporting success.

```bash
# From the repo root
harness/run.sh "task description" [--commit auto] [--bump]
```

- Runs the task with an `opencode` agent (DeepSeek model by default).
- Applies the verification gates (`harness/verify.sh`): build, tests and frontend checks. Any failure reverts the changes (unless `--keep-changes`).
- After a successful verification it can create a conventional commit (`--commit`) and bump SemVer (`--bump`).
- Main options: `-m/--model`, `-a/--agent`, `--no-verify`, `--keep-changes`, `--commit <type>`, `--bump [auto|major|minor|patch]`, `--title <text>`, `--issue <n>` / `--pr <n>`. See help with `-h` or `harness/run.sh --help`.
- Logs and reports are written to `harness/logs/` (session, verification and summary).
