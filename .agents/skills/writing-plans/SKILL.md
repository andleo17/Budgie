---
name: writing-plans
description: "Usa después de brainstorming/tras aprobar una design spec, para convertirla en un implementation plan en docs/plans/ con trazabilidad exacta a archivos de código ([MODIFY]/[NEW]). Es el paso que invoca el terminal state de brainstorming."
---

# Writing Plans (Budgie)

Conviertes una **design spec aprobada** en un **implementation plan** en `docs/plans/`.

Es el **único** skill que invoca `brainstorming` al terminar. Antes: se debe haber aprobado la
design spec (`docs/superpowers/specs/`). Después: `implementing`.

## Entrada
- Design spec aprobada. Léela completa.
- `AGENTS.md` (convenciones de capas, naming, CQRS, repos).
- Estado del repo: `git log --oneline -10`, estructura de `src/core`, `src/apps`, `tests`.

## Proceso

1. **Descompón** en unidades pequeñas por agregado/capa (Clean Architecture). Una unidad = un slice
   que un solo implementer pueda hacer sin perder contexto.
2. Por cada unidad define: objetivo, archivos afectados y pasos concretos.
3. Aplica **trazabilidad estricta**: cada archivo con `[MODIFY]` o `[NEW]` y ruta completa real.
   Nunca dejes `.../Archivo.cs` sin expandir.
4. Añade el **Verification Plan** por superficie: `dotnet build/test` (backend), `tsc/lint/build`
   (frontend), `bunx playwright` (e2e).

## Estructura del plan

```markdown
# Implementation Plan — <Feature>

## User Review Required (si aplica)
> [!NOTE]
> Decisiones de spec ya integradas: <lista>

## Proposed Changes

### 1. <Unidad> (<Módulo/Layer>)
- **Objetivo**: <frase>
- **Archivos**:
  - `[MODIFY] src/core/Budgie.Modules.Xxx/.../X.cs`
  - `[NEW] src/core/Budgie.Modules.Xxx/.../X.cs`
- Pasos: <bullets>
...

## Verification Plan
- `dotnet build Budgie.slnx`
- `dotnet test Budgie.slnx`
- `cd src/apps/Budgie.Web && bun run build`
```

## Salida
Escribe `docs/plans/<nn>-<feature>.md`. Si es el plan maestro, `docs/plans/00-implementation_plan.md`.

## Reglas
- **No implementes**. Solo el plan y su estrategia de verificación.
- **Unidades pequeñas y direccionables**. 
- Sigue las convenciones del módulo (no inventes pasos fuera del patrón existente).
