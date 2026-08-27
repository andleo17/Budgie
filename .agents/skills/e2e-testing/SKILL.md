---
name: e2e-testing
description: "Usa tras implementar código frontend, para añadir/ejecutar tests E2E con Playwright en src/apps/Budgie.Web/e2e/ contra una API mockeada (MSW/OpenAPI). Delega en frontend-e2e."
---

# E2E Testing (Budgie — frontend)

Tras implementar UI, añades **tests end-to-end con Playwright** contra frontend real y **API
mockeada** (determinista, sin PostgreSQL ni API AOT levantada — eficiente en recursos).

## Cuándo
- Después de `implementing` con cambios en `src/apps/Budgie.Web/**`.
- Solo E2E (sin runner de componentes en el repo).

## Infraestructura

`src/apps/Budgie.Web`:
- `playwright.config.ts` — baseURL al dev server, proyecto Chromium, reporter `list`.
- `e2e/` — specs (`.spec.ts`), `mocks/` handlers OpenAPI, `fixtures/`.
- Mocks: intercepta `/api/**` con handlers derivados de `openapi.json`/`api-types.ts`.
- Script: `test:e2e` → `bunx playwright test`.

## Proceso

1. Identifica el flujo de usuario tocado por el cambio.
2. **Delega** a `frontend-e2e` para escribir/actualizar specs.
3. Asegúrate de:
   - Selección por rol/accesibilidad (`getByRole`, `getByLabel`), no test-id frágil.
   - Seed determinista por test; reset en `beforeEach`.
   - Sin `waitForTimeout`; usa autowait (`toBeVisible`).
   - Viewport móvil (`375x667`) cubierto en flujos clave.
   - i18n cubierta (`es`/`en`).
4. Ejecuta: `cd src/apps/Budgie.Web && bunx playwright test`.

## Reglas
- Nunca golpear backend real; siempre mock.
- No añadas infra/specs fuera de alcance (YAGNI).
- Corre los specs que escribas y corrige fallos de tu slice.
