---
name: sdd
description: "Máster del ciclo Specification-Driven Development de Budgie. Úsala como herramienta de orquestación para decidir el siguiente paso de un cambio (brainstorming, writing-plans, implementing, writing-tests/e2e-testing, code-review, verifying o release), enrutar el trabajo a sub-agents backend/frontend y aplicar los quality gates."
---

# SDD — Specification-Driven Development (Budgie)

Eres el **orquestador** del ciclo SDD de Budgie. El proyecto se desarrolla **spec-first**:
nada de código se escribe sin un diseño y un plan aprobados. Tu trabajo es *dirigir* el flujo,
delegar el trabajo pesado a sub-agents y hacer respetar los gates.

## Ciclo SDD

```dot
digraph sdd {
  "brainstorming" [shape=box];
  "writing-plans" [shape=box];
  "implementing" [shape=box];
  "writing-tests" [shape=box];
  "e2e-testing" [shape=box];
  "code-review" [shape=box];
  "verifying" [shape=box];
  "release" [shape=box, style=bold];

  "brainstorming" -> "writing-plans";
  "writing-plans" -> "implementing";
  "implementing" -> "writing-tests";
  "writing-tests" -> "code-review";
  "e2e-testing" -> "code-review";
  "implementing" -> "e2e-testing";
  "code-review" -> "verifying";
  "verifying" -> "release";
}
```

### Cuándo usar cada paso
| Paso | Entrada | Salida | Sub-agent |
|------|---------|--------|-----------|
| `brainstorming` | idea | design spec (`docs/superpowers/specs/`) | (interactivo, primario) |
| `writing-plans` | design spec aprobada | `docs/plans/` con trazabilidad | `plan-writer` |
| `implementing` | slice de plan | código en `src/**` | `backend-implementer` / `frontend-implementer` |
| `writing-tests` | código backend | tests ~100% cobertura | `backend-test-author` |
| `e2e-testing` | código frontend | specs Playwright (API mockeada) | `frontend-e2e` |
| `code-review` | diff | reporte de convenciones | `backend-reviewer` / `frontend-reviewer` |
| `verifying` | código | PASS/FAIL de gates | `verify` |
| `release` | verificación verde | Conventional Commit + SemVer | (primario, comando) |

## HARD-GATE

**No implementes** hasta que existan: (1) design spec aprobada y (2) implementation plan aprobado.
Un cambio "trivial" sigue el ciclo (el diseño puede ser de pocas frases).

## Enrutamiento backend vs frontend

Decide el grupo según las rutas que toca el slice:

- **Backend**: `src/core/**`, `src/apps/Budgie.Api/**`, `tests/Budgie.*.UnitTests/`, `tests/Budgie.IntegrationTests/` → `backend-implementer`, `backend-reviewer`, `backend-test-author`.
- **Frontend**: `src/apps/Budgie.Web/**` → `frontend-implementer`, `frontend-reviewer`, `frontend-e2e`.
- **Transversal**: `docs/**`, `AGENTS.md`, esquemas SQL → usa el agente primario, o delega a `plan-writer`/`verify` según el paso.

Un cambio que toca ambos → delega en dos slices separadas (backend luego frontend).

## Protocolo de clarificación

Si el sub-agente necesita una aclaración del usuario, reláyala tal cual y espera respuesta,
retomando la misma sesión. Nunca inventes la respuesta.

## Quality gates (antes de `release`)

1. `dotnet build Budgie.slnx` (si hubo backend).
2. `dotnet test Budgie.slnx` (si hubo backend/tests).
3. `tsc --noEmit` + `bun lint` + `bun build` (si hubo frontend).
4. `bunx playwright test` (si hubo frontend e2e).
5. Todo debe estar **verde** antes de commitear.

## Reglas transversales

- **Trazabilidad**: cada artifacts enlaza `docs/specs` y rutas de código reales.
- **YAGNI**: sin infra ni features extra fuera del plan.
- **Eficiencia**: delega a sub-agents para mantener tu contexto pequeño; usa los gates en vez de
  iterar a ciegas.
- **Commits**: Conventional Commits (tipos/scopes en `docs/specs/06`).
