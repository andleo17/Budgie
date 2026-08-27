---
name: writing-tests
description: "Usa tras implementar código backend, para añadir tests de unidad e integración (xUnit, NSubstitute, FluentAssertions, WebApplicationFactory + PostgreSQL) y alcanzar ~100% de cobertura según docs/specs/06. Delega en backend-test-author."
---

# Writing Tests (Budgie — backend)

Tras implementar un slice backend, añades **tests** para cumplir la estrategia de
`docs/specs/06-testing-strategy-and-git-conventions.md` (~100% cobertura).

## Cuándo
- Después de `implementing` con cambios en `src/core/**` o `src/apps/Budgie.Api/**`.
- No aplica al frontend (sin runner de componentes; usa `e2e-testing` para UI).

## Proceso

1. Identifica la superficie tocada (handler, entity, value object, evento).
2. **Delega** a `backend-test-author` para escribir/actualizar tests.
3. Revisa la cobertura: 
   - Cada rama: éxito + cada error `ErrorOr` (`NotFound`, `Conflict`, `Unauthorized`,
     `Forbidden`, `Validation`) → assert `result.FirstError.Code`.
   - Multi-tenancy: siembra segundo usuario → assert exclusión.
   - Límites/edge: decimales exactos, valores frontera.
   - i18n: `es` y `en`.
4. Corre los tests: `dotnet test <proyecto>` y todo `dotnet test Budgie.slnx`.

## Estructura
- Unit: `tests/Budgie.Modules.Xxx.UnitTests/Application/<Feature>/<Action>/<Action>CommandHandlerTests.cs`
- Domain: `tests/Budgie.Modules.Xxx.UnitTests/Domain/<Aggregate>/<Entity>Tests.cs`
- Integration: `tests/Budgie.IntegrationTests/`

## Reglas
- Naming: `Handle_<Scenario>_<Expected>()`, domain `<Behavior>_Should<Expected>()`.
- EF InMemory no traduce nav+VO ni SQL raw → cubre la rama InMemory-safe; lo demás por integration
  tests (PostgreSQL real).
- No "test de cobertura por cumplir": cada test debe verificar comportamiento, no ser un saco.
