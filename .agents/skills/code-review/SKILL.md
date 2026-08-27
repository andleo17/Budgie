---
name: code-review
description: "Usa tras implementar (y testear), para revisar el diff contra las convenciones del repo antes de verificar/commitear. Enruta a backend-reviewer (C#) o frontend-reviewer (TSX/UI) según las rutas tocadas."
---

# Code Review (Budgie)

Revisas un **diff** contra las convenciones del repositorio **antes** de verificar y commitear.

## Proceso

1. Obtén el rango: `git log --oneline -5` y `git diff <base>...HEAD --stat`.
2. Clasifica el diff por rutas:
   - `src/core/**`, `src/apps/Budgie.Api/**`, `tests/Budgie.*.UnitTests/`, `tests/Budgie.IntegrationTests/` → **backend**.
   - `src/apps/Budgie.Web/**` → **frontend**.
3. **Delega** la revisión por grupo (pueden ser dos, si el diff toca ambos):
   - `backend-reviewer` (contra `AGENTS.md` + `dotnet-best-practices` + `dotnet-design-pattern-review`).
   - `frontend-reviewer` (contra `react-best-practices` + `web-design-guidelines`).
4. Consolida los reportes. **No continúes** si hay críticas o medias sin resolver.

## Parámetros
- Fuente de verdad de convenciones: `AGENTS.md`.
- Severidades: **Crítica** (bloquea), **Media** (corregir), **Menor** (opcional).
- Cada observación referencia `archivo:línea`.

## Reglas
- La revisión es **solo lectura** (los reviewers tienen `edit: deny`).
- Evalúa únicamente el diff; no propongas refactors fuera de alcance.
- Aprobación = sin críticas ni medias.
