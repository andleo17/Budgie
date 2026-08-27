---
name: implementing
description: "Usa para ejecutar un implementation plan aprobado, slice a slice, siguiendo AGENTS.md. Delega cada unidad al sub-agent adecuado (backend-implementer para src/core y Budgie.Api, frontend-implementer para Budgie.Web) y mantiene el progreso del plan."
---

# Implementing (Budgie)

Ejecutas un **implementation plan** aprobado (`docs/plans/`), **un slice a la vez**.

## Checklist por slice

1. Lee el slice del plan y su spec.
2. Relee el patrón del módulo (archivo vecino) para replicarlo.
3. Carga la skill de dominio del stack:
   - Backend → `create-module` (módulos nuevos), `dotnet-10-csharp-14`.
   - Frontend → `react-best-practices`, `shadcn`, `web-design-guidelines` (+ `frontend-design`/
     `ui-ux-pro-max` si requiere diseño).
4. **Delega** el slice al sub-agent correcto y valida su salida:
   - Backend → `backend-implementer`. Compila: `dotnet build Budgie.slnx`.
   - Frontend → `frontend-implementer`. Valida: `bun run typecheck` + `bun run lint`.
5. Actualiza el plan marcando el slice como hecho (o añade nota si requirió ajuste).

## Enrutamiento por rutas

| Rutas | Sub-agent |
|-------|-----------|
| `src/core/**`, `src/apps/Budgie.Api/**` | `backend-implementer` |
| `src/apps/Budgie.Web/**` | `frontend-implementer` |
| `docs/**`, SQL schema, AGENTS.md | primario (no delega) |

Un slice que toca backend y frontend → divídelo en dos delegaciones.

## Reglas
- Implementa **solo** lo del plan (YAGNI). No refactors fuera de alcance.
- Respeta el **hard-gate**: el plan debe estar aprobado.
- **No commitees** en este paso; eso llega en `release` tras `verifying`.
