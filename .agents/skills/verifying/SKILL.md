---
name: verifying
description: "Usa antes de commitear/releasear, para correr las gates de calidad (dotnet build/test, tsc, bun lint, bun build, bunx playwright) y exigir todo verde. Delega en verify y reporta PASS/FAIL."
---

# Verifying (Budgie)

Ejecutas las **gates de calidad** y solo avanzas si todo pasa. Es el sucesor de `harness/verify.sh`.

## Gates (desde la raíz del repo)

Ejecuta solo las relevantes al diff; para máxima seguridad corre todas:

| Gate | Comando | Cuando |
|------|---------|--------|
| Backend build | `dotnet build Budgie.slnx --nologo` | si hubo `src/core/**` o `Budgie.Api` |
| Backend test | `dotnet test Budgie.slnx --no-build --nologo` | si hubo backend/tests |
| Frontend typecheck | `bash -c "cd src/apps/Budgie.Web && bunx tsc --noEmit -p tsconfig.json"` | si hubo `Budgie.Web` |
| Frontend lint | `bash -c "cd src/apps/Budgie.Web && bun run lint"` | idem |
| Frontend build | `bash -c "cd src/apps/Budgie.Web && bun run build"` | idem |
| E2E Playwright | `bash -c "cd src/apps/Budgie.Web && bunx playwright test"` | si hubo `e2e/` |

## Proceso

1. **Delega** a `verify` (o corre las gates con su aprobación).
2. Revisa el reporte. Cada FAIL debe incluir su diagnóstico raíz (no solo "salió mal").
3. Que todo esté **verde** antes de `release`. Si detecta un fallo real de código, vuelve a
   `implementing` para corregirlo (no commitees sobre rojo).
4. Distingue fallos de entorno (falta `bun`/deps/Postgres) de regresiones: los de entorno se
   resuelven, no se "corrigen" en código.

## Reglas
- El paso es **puerta de calidad**: rojo = no release.
- Eficiencia: puedes ejecutar solo las gates del alcance; en el gate de release corre todo.
