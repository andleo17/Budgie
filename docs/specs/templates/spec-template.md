# <Topic> — Design Spec

**Fecha:** YYYY-MM-DD
**Estado:** Aprobado por el usuario

## Objetivo

<Una frase clara de lo que se quiere lograr y para quién.>

## Decisiones previas (con el usuario)

1. <decisión clave 1>
2. <decisión clave 2>

## Contexto

<Breve contexto: qué existe hoy, qué problema resuelve. Referencia specs/AGENTS.md y rutas reales del repo.>

## Arquitectura

### <Unidad 1>

- **Qué hace**: <una frase>
- **Dónde**: `src/core/Budgie.Modules.Xxx/...` (ruta real)
- **Cómo se usa**: <cómo se consume>
- **Dependencias**: <de qué depende>

### <Unidad 2>

...

## Flujo de datos

<Describe el flujo concreto, de entrada a persistencia/salida. Menciona CQRS/Eventos si aplica.>

## Manejo de errores

- <Error 1> → `Error.Unsupported("Xxx.Codigo", "mensaje")`
- <Error 2> → ...

## Testeo

- Unit: `tests/Budgie.Modules.Xxx.UnitTests/...`
- Integration: `tests/Budgie.IntegrationTests/...`
- E2E (si UI): `src/apps/Budgie.Web/e2e/...`

## Fuera de alcance

<Lo que explícitamente NO se hace en esta spec.>

## Riesgos

- <riesgo> → <mitigación>
