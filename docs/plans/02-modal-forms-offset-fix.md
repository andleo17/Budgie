# Implementation Plan — Modal Forms Offset Fix (DialogContent gap-0)

> Spec: `docs/superpowers/specs/2026-09-09-modal-forms-offset-fix-design.md` (Aprobada 2026-09-09, Scope `src/apps/Budgie.Web`)
> SRS refs: `docs/specs/07-sdd-methodology.md` §6 (gates), `AGENTS.md` (Project Structure, Module Structure — `src/apps/Budgie.Web` React+Base UI)
> Repo: `Budgie.slnx` (.NET 10 AOT + Vite), `src/apps/Budgie.Web` (React 19 + Base UI Dialog, shadcn/ui), working-tree fix parcial ya presente

---

## User Review Required

> [!NOTE]
> Decisiones de spec ya integradas y que condicionan el plan (hard-gate SDD — requiere aprobación antes de `implementing`):
> - **Opción A aprobada (fix centralizado)**: 1 archivo `DialogContent` → `gap-4 → gap-0` + `sm:pb-4 → sm:gap-0 sm:p-4`. Opciones B (tocar 8+ formularios) y C (flex con scroll separado) descartadas por riesgo/escala (spec §3).
> - **Causa raíz fijada**: `DialogContent` Popup `grid gap-4` (línea 56) + formularios `grid gap-4 py-4` = 32px offset (16 gap + 16 py-top) visible solo en desktop `>=sm` centrado. Mobile bottom-sheet OK (spec §1, P1).
> - **Contrato de clases exacto** (spec §4.3): Mobile `gap-0 / p-4` y Desktop `sm:gap-0 sm:p-4` (no `sm:pb-4` solo). `DialogHeader` mantiene `gap-2` interno. `DialogOverlay`, focus trap Base UI y `onOpenChange` sin cambios.
> - **Sin cambios en formularios**: `IncomeExpenseForm`, `TransferForm`, `LoanForm`, `CollectionPaymentForm`, `AccountFormDialog`, `CategoryFormDialog`, `GoalFormDialog`, `PersonFormDialog`, `TransactionFormDialog` mantienen `className="grid gap-4 py-4"` — solo verification visual, no `[MODIFY]` (spec §4.2).
> - **Working-tree ya contiene el fix** (`git diff HEAD -- src/apps/Budgie.Web/src/components/ui/dialog.tsx`): `gap-4→gap-0` (línea 56) y `sm:pb-4 → sm:gap-0 sm:p-4` (línea 60). El plan formaliza el diff en slice implementable + regression guard.
> - **Regression guard existente** (no crear nuevo test): `tests/Budgie.Modules.Finances.UnitTests/Infrastructure/DialogLayoutRegressionTests.cs:30` — `DialogContent_ShouldNotHaveGap4_ShouldUseGap0` aserta `gap-0` presente y `w-full gap-4 overflow` ausente. Si reaparece `gap-4`, falla.
> - **Fuera de alcance explícito**: backend en working-tree (`TransactionRepository` cuotas, `CreateTransactionCommandHandler`, `DateAndFilterRegressionTests`, etc.) y `Transactions.tsx` pertenecen a spec Money Manager import — no tocar en este plan (spec §5).

Requiere `plan-writer` approval → `implementing` (frontend-implementer slice único). Solo `docs/plans/` editable (no código).

---

## Contexto repo verificado (entrada del plan)

- `git log --oneline -10` (2026-09-09, rama `main`): `ffdc513 feat(finances): import Money Manager .mmbak`, `6460812 test(web): add playwright e2e`, `5049659 feat(web): complete mobile-native redesign` — base estable del bug es `5049659` + refinamientos `fd781e1/7a662b5`.
- `git status`: `docs/superpowers/specs/2026-09-09-modal-forms-offset-fix-design.md` staged; `src/apps/Budgie.Web/src/components/ui/dialog.tsx` modificado (gap-0 fix) no stageado; regression test `DialogLayoutRegressionTests.cs` untracked (ya existe en working-tree).
- Estructura `src/apps/Budgie.Web/src/components/ui/` (32 archivos): `dialog.tsx` (4925 B, `DialogContent` línea 40, `DialogPrimitive.Popup` con `cn()`), `alert-dialog.tsx`, `sheet.tsx`, `button.tsx` etc. — confirma ruta real `src/apps/Budgie.Web/src/components/ui/dialog.tsx:40`.
- `docs/plans/` existe: `00-implementation_plan.md` + `01-money-manager-import.md` → siguiente es `02-modal-forms-offset-fix.md`.

---

## Tareas (una por unidad de implementación)

> Slice único frontend (no backend). Un implementer (frontend-implementer) ejecuta sin perder contexto. Si se detecta drift, re-leer `AGENTS.md` §Module Structure (Presentation layer).

### 1. Fix centralizado DialogContent — gap-0 + padding consistente (Frontend / `src/apps/Budgie.Web` — Presentation primitive)

- **Objetivo**: Eliminar 32px offset en desktop haciendo `DialogContent` Popup `gap-0` en mobile y `sm:gap-0 sm:p-4` en desktop, sin tocar formularios.
- **Archivos**:
  - `[MODIFY] src/apps/Budgie.Web/src/components/ui/dialog.tsx` — única línea de cambio real (Popup `className` vía `cn()`). Líneas 53-62 actuales ya contienen el fix en working-tree; el slice consiste en consolidar y verificar el diff contra HEAD.
  - Verificación sin cambios (listar como superficies de regression visual, no editar):
    - `src/apps/Budgie.Web/src/modules/transactions/components/IncomeExpenseForm.tsx` (usa `grid gap-4 py-4`)
    - `src/apps/Budgie.Web/src/modules/transactions/components/TransferForm.tsx`
    - `src/apps/Budgie.Web/src/modules/transactions/components/LoanForm.tsx`
    - `src/apps/Budgie.Web/src/modules/transactions/components/CollectionPaymentForm.tsx`
    - `src/apps/Budgie.Web/src/modules/transactions/components/TransactionFormDialog.tsx`
    - `src/apps/Budgie.Web/src/modules/accounts/components/AccountFormDialog.tsx` (si existe como `AccountFormDialog`)
    - `src/apps/Budgie.Web/src/modules/categories/components/CategoryFormDialog.tsx` (idem)
    - `src/apps/Budgie.Web/src/modules/goals/components/GoalFormDialog.tsx` (idem)
    - `src/apps/Budgie.Web/src/modules/persons/components/PersonFormDialog.tsx` (idem)
  - Regression guard existente (no modificar, solo ejecutar):
    - `tests/Budgie.Modules.Finances.UnitTests/Infrastructure/DialogLayoutRegressionTests.cs` — ya cubre el contrato.

- **Pasos concretos** (bullets accionables, referenciando patrón existente `src/apps/Budgie.Web/src/components/ui/dialog.tsx` + `cn()` + Base UI):
  - 1. Abrir `src/apps/Budgie.Web/src/components/ui/dialog.tsx:40` (`DialogContent` function) y localizar `DialogPrimitive.Popup` `className={cn( ... )}` líneas 53-64. Confirmar HEAD bug: `"fixed inset-x-0 bottom-0 z-50 grid max-h-[90dvh] w-full gap-4 overflow-y-auto ..."` + `"sm:... sm:pb-4 ..."`.
  - 2. Aplicar contrato spec §4.3 exacto (2 líneas, ya en working-tree — re-aplicar si se hizo `git restore`):
    ```tsx
    // Mobile (línea 56): gap-4 → gap-0 (mantener resto idéntico)
    "fixed inset-x-0 bottom-0 z-50 grid max-h-[90dvh] w-full gap-0 overflow-y-auto overscroll-contain rounded-t-2xl bg-popover p-4 pb-safe text-sm text-popover-foreground ring-1 ring-foreground/10 transition-transform duration-200 ease-out outline-none",
    // Desktop (línea 60): sm:pb-4 → sm:gap-0 sm:p-4 (insertar sm:gap-0 antes de sm:p-4, mantener sm:rounded-xl sm:overflow-visible)
    "sm:top-1/2 sm:left-1/2 sm:bottom-auto sm:max-h-none sm:w-auto sm:max-w-[calc(100%-2rem)] sm:-translate-x-1/2 sm:-translate-y-1/2 sm:rounded-xl sm:overflow-visible sm:gap-0 sm:p-4 sm:transition-none sm:before:hidden",
    ```
  - 3. Verificar que `DialogHeader` (`src/apps/Budgie.Web/src/components/ui/dialog.tsx:89`) sigue `className={cn("flex flex-col gap-2", ...)}` — no tocar (gap-2 título→descripción intencional).
  - 4. Verificar `DialogFooter` (`src/apps/Budgie.Web/src/components/ui/dialog.tsx:99`): mantiene `-mx-4 -mb-4 ... p-4 sm:flex-row sm:justify-end` — depende del nuevo `p-4` del Popup para compensar márgenes negativos; no tocar.
  - 5. Confirmar que no queda `w-full gap-4 overflow` en el archivo (grep `gap-4` solo debe aparecer si es `sm:gap-0` o en `DialogHeader/Footer` con `gap-2`; `gap-4` del Popup debe desaparecer).
  - 6. Confirmar `DialogOverlay` sin cambios (`src/apps/Budgie.Web/src/components/ui/dialog.tsx:24`): `fixed inset-0 ... bg-black/10` — no tocar.
  - 7. Ejecutar `git diff HEAD -- src/apps/Budgie.Web/src/components/ui/dialog.tsx` y validar que solo hay 2 hunk: `gap-4→gap-0` y `sm:pb-4→sm:gap-0 sm:p-4`. Si el diff incluye cambios colaterales (`Transactions.tsx`, `CreateTransactionCommandHandler.cs`, etc.), no commitear en este slice — pertenecen a otra spec.
  - 8. No introducir nuevos componentes, wrappers ni props. Mantener `showCloseButton` y `children` passthrough. Seguir convención `cn()` + `data-slot="dialog-content"` ya existente (no `clsx` directo).
  - 9. Marcar formularios listados arriba como "verification only": abrir cada uno en viewport desktop `>=640px` y comprobar visualmente que primer field está a 16px (`p-4` del Popup + `py-4` del form = 16px top, sin 32px). Mobile `<640px` bottom-sheet con drag handle `before:h-1.5 w-10` debe seguir `gap-0` sin doble separación.

- **Criterio de done de la tarea**:
  - `dialog.tsx` contiene `gap-0` (mobile) y `sm:gap-0 sm:p-4` (desktop), no contiene `w-full gap-4 overflow`.
  - `DialogLayoutRegressionTests.DialogContent_ShouldNotHaveGap4_ShouldUseGap0` en verde (previene reintroducción).
  - `bun run build` verde (ver Verification Plan).

---

## Verification Plan

> Hard-gate SDD `docs/specs/07-sdd-methodology.md §6`: todo verde antes de `release`. Slice frontend → gates frontend + regression backend (test existente). Distinguir fallo de entorno (sin `bun`/deps) de regresión.

### 1. Frontend gates (obligatorios para este slice — `src/apps/Budgie.Web`)

Ejecutar desde `src/apps/Budgie.Web/` (Node 20+, `bun` disponible — `bun.lock` presente; si `bun` no está, usar `npm` equivalente pero reportar como entorno no regresión):

```bash
cd src/apps/Budgie.Web
# Typecheck estricto (tsc -b --noEmit) — debe pasar sin errores; valida tipos DialogPrimitive.Popup props
bun run build
# Alternativa explícita si build oculta tsc:
npx tsc -b --noEmit
# Lint (eslint . con config eslint.config.js)
bun run lint
# Build Vite (tsc -b && vite build) — genera dist/ sin errores
bun run build
```

- **Esperado**: `typecheck` 0 errors, `lint` 0 warnings/errors en `src/components/ui/dialog.tsx`, `build` produce `dist/` con `dialog-*.js` chunk.
- **Si falla**: no release; corregir clases (typo `sm:gap-0`, `sm:p-4`) antes de `verifying`.

Opcional visual manual (no bloqueante para CI pero recomendado):
- Abrir `http://localhost:5173` (o `5174`) con `bun run dev`, viewport `1280x800` (`>=sm`), abrir cualquier modal (`TransactionFormDialog` vía botón "Nueva transacción") y medir con DevTools: distancia `DialogHeader` bottom → primer `Input` top = 16px (`p-4` exterior + `py-4` interior sin gap). Repetir en viewport `375x812` mobile: bottom-sheet `gap-0` ok, drag handle visible.

### 2. Backend regression guard (test existente — no nuevo código backend)

Aunque el slice no toca backend, el regression test vive en `tests/Budgie.Modules.Finances.UnitTests`:

```bash
# Desde repo root
dotnet build Budgie.slnx --nologo
dotnet test Budgie.slnx --verbosity normal --filter "DialogLayoutRegressionTests"
# O filtrado por clase/método:
dotnet test tests/Budgie.Modules.Finances.UnitTests/Budgie.Modules.Finances.UnitTests.csproj --filter "DialogContent_ShouldNotHaveGap4_ShouldUseGap0" --verbosity normal
```

- **Esperado**: `DialogLayoutRegressionTests.DialogContent_ShouldNotHaveGap4_ShouldUseGap0` PASS (aserta `gap-0` presente, `w-full gap-4 overflow` ausente). Los otros 2 tests en el mismo archivo (`TransactionRepository_ShouldPersistInstallmentFields`, `CreateTransactionHandler_ShouldHandleInstallmentsSplitting`) pertenecen a import feature — pueden estar FAIL si DDL no aplicado; no bloquean este fix pero deben reportarse como fuera de alcance (spec §5). El gate relevante es solo el primer test.
- **Full suite** (si se quiere verde completo):
  ```bash
  dotnet test Budgie.slnx --verbosity normal
  dotnet test Budgie.slnx --collect:"XPlat Code Coverage"
  ```
  Cobertura no requerida para frontend slice; backend sin cambios → suite debe permanecer verde salvo tests colaterales no commiteados.

### 3. E2E (no requerido para este fix)

- No se requiere nuevo Playwright spec (bug puramente CSS `gap`). Si se desea snapshot, añadir en futuro `src/apps/Budgie.Web/e2e/dialog-offset.spec.ts` con viewport `1280x800` snapshot de `DialogContent` — fuera de alcance de este plan.
- Si `e2e/` se ejecuta, `bunx playwright test` debe seguir verde (mocks MSW no afectados).

### 4. Criterios de fallo (no release)

- `bun run build` / `lint` / `typecheck` rojo → FAIL (regresión frontend).
- `dotnet test --filter DialogLayoutRegressionTests` rojo en `DialogContent_ShouldNotHaveGap4_ShouldUseGap0` → FAIL (reintrodujo `gap-4`).
- `git diff HEAD -- src/apps/Budgie.Web/src/components/ui/dialog.tsx` muestra cambios fuera de las 2 líneas de contrato → FAIL (scope creep).

---

## Trazabilidad final

| Spec § | Slice | Archivos clave |
|---|---|---|
| §1 Contexto bug gap-4+py-4=32px, P1 solo desktop | 1 | `src/apps/Budgie.Web/src/components/ui/dialog.tsx:40-62` |
| §3 Opción A (1 archivo centralizado) | 1 | `[MODIFY] src/apps/Budgie.Web/src/components/ui/dialog.tsx` |
| §4.1 Single source DialogContent, sin nuevos componentes | 1 | `src/apps/Budgie.Web/src/components/ui/dialog.tsx:51` (`DialogPrimitive.Popup`) |
| §4.3 Contrato gap-0 / sm:gap-0 sm:p-4, mobile p-4 pb-safe rounded-t-2xl | 1 | `src/apps/Budgie.Web/src/components/ui/dialog.tsx:56,60` |
| §4.6 Regression guard DialogLayoutRegressionTests:30 | 1 | `tests/Budgie.Modules.Finances.UnitTests/Infrastructure/DialogLayoutRegressionTests.cs:30` |
| §5 Fuera alcance (backend cuotas, Transactions.tsx) | — | No tocar: `src/core/Budgie.Modules.Finances/Infrastructure/Database/TransactionRepository.cs`, `CreateTransactionCommandHandler.cs`, `src/apps/Budgie.Web/src/modules/transactions/**` |
| §7 Done: gap-0 + sm:gap-0 sm:p-4, test verde, build verde | 1 | Verification Plan arriba |

---

## Notas para implementers

- **No implementar hasta aprobación del plan** (SDD hard-gate). Frontend-implementer toma slice 1; backend-implementer no interviene.
- Respetar `AGENTS.md`: `src/apps/Budgie.Web` usa `cn()` + `shadcn/ui` + `Base UI DialogPrimitive`; no introducir `clsx` directo ni lógica TS. Solo `docs/plans/` editable en este paso (plan-writer).
- El working-tree ya tiene el fix; `implementing` debe consolidar el diff y asegurar que `git status` solo muestre `dialog.tsx` modificado para este commit `fix(web): dialog gap-0 remove form offset`. No mezclar con `Transactions.tsx` / `TransactionRepository.cs` (otro commit `feat(finances): installments`).
- Si `bun` no está en CI local, `verifying` debe distinguir "entorno" vs "regresión" (ver `07-sdd-methodology.md §6`).
