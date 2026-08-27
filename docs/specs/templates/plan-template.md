# Implementation Plan — <Feature>

## User Review Required (si aplica)
> [!NOTE]
> Decisiones de la spec ya integradas: <lista de decisiones clave>

## Tareas (una unidad = un slice por implementer)

### 1. <Unidad> (<Módulo / Layer>)
- **Objetivo**: <frase>
- **Archivos**:
  - `[MODIFY] src/core/Budgie.Modules.Xxx/.../Archivo.cs`
  - `[NEW] src/core/Budgie.Modules.Xxx/.../Archivo.cs`
- **Pasos**:
  - <paso concreto y accionable>
  - <paso>

### 2. <Unidad> (...)

- ...

## Verification Plan

### Backend (si toca `src/core/**` o `src/apps/Budgie.Api/**`)
- `dotnet build Budgie.slnx --nologo`
- `dotnet test Budgie.slnx --no-build --nologo`
- (integración / cobertura) `dotnet test --collect:"XPlat Code Coverage"`

### Frontend (si toca `src/apps/Budgie.Web/**`)
- `bash -c "cd src/apps/Budgie.Web && bunx tsc --noEmit -p tsconfig.json"`
- `bash -c "cd src/apps/Budgie.Web && bun run lint"`
- `bash -c "cd src/apps/Budgie.Web && bun run build"`
- (si hubo E2E) `bash -c "cd src/apps/Budgie.Web && bunx playwright test"`
