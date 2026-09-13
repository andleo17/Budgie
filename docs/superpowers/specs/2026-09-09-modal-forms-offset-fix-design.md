# Fix: Formularios movidos hacia abajo en modales (web escritorio) — Design

**Fecha:** 2026-09-09
**Estado:** Aprobado (SDD Fase 1 — fix UI)
**Scope:** `src/apps/Budgie.Web` — `DialogContent` + formularios en modales
**Afecta:** Todos los diálogos que usan `DialogContent` en breakpoint `sm:` (desktop)

## 1) Contexto y problema

Tras el rediseño mobile-native (spec `2026-08-26-mobile-native-redesign-design.md`), `DialogContent` (`src/apps/Budgie.Web/src/components/ui/dialog.tsx:40`) se convirtió en bottom-sheet en mobile (`< sm`) y diálogo centrado en desktop (`>= sm`) via clases responsivas.

**Bug en desktop:** los formularios dentro de modales aparecen desplazados hacia abajo ~16-32px extra.

**Causa raíz identificada (HEAD vs working-tree):**
- `DialogContent` usa layout `grid` con `gap-4` (mobile) sin override en desktop → en desktop persiste `gap-4` entre `DialogHeader` → formulario → `DialogFooter`.
- Cada formulario interno (`IncomeExpenseForm`, `TransferForm`, `LoanForm`, `CollectionPaymentForm`, `AccountFormDialog`, `CategoryFormDialog`, `GoalFormDialog`, `PersonFormDialog`, `TransactionFormDialog`) además tiene `className="grid gap-4 py-4"` o `py-4` como padding interno.
- Combinación: `gap-4` del Popup (16px) + `py-4` del form (16px top) = 32px de offset percibido entre header y primer field. En desktop centrado el gap se nota como "form hundido". En HEAD además desktop solo tenía `sm:pb-4` (solo bottom), no `sm:p-4`, dejando inconsistencia de padding vs mobile.

Evidencia en `git diff HEAD` actual (no commiteado): el fix ya iniciado corrige `gap-4 → gap-0` y `sm:pb-4 → sm:gap-0 sm:p-4`.

## 2) Preguntas de clarificación (proceso SDD)

**P1:** ¿El desplazamiento ocurre solo en escritorio (`>= 640px`) o también en mobile?
> Respuesta (issue): solo modales en web escritorio. Mobile bottom-sheet está OK (drag handle, `p-4`, `gap-0` deseado allí para scroll interno).

**P2:** ¿Qué diálogos están afectados?
> Todos los que usan `DialogContent`: transacciones (Income/Expense, Transfer, Loan, Collection, Delete, Templates), cuentas, categorías, personas, goals, settings. Único primitivo → fix centralizado.

**P3:** ¿Criterio de éxito?
> En desktop el contenido del formulario inicia a `p-4` (16px) bajo el `DialogHeader` sin gap adicional; visual parity con mobile pero centrado. No regresión en mobile bottom-sheet.

## 3) Enfoques evaluados (2-3)

### A) Fix centralizado en `DialogContent` — gap-0 + padding consistente (Recomendado)
- Cambiar `gap-4 → gap-0` en base (mobile) y añadir `sm:gap-0` + `sm:p-4` en desktop.
- Mantener `gap-2` en `DialogHeader` (título → descripción) pero eliminar gap entre secciones del Popup.
- **Pros:** 1 archivo, todos los diálogos heredan fix, sin tocar formularios. Ya validado en working-tree.
- **Contras:** Ninguno relevante.
- **Esfuerzo:** ~2 líneas.

### B) Quitar `py-4` de los formularios, mantener `gap-4` en DialogContent
- Editar 8+ formularios para eliminar `py-4` / `gap-4` internos.
- **Pros:** También reduce offset.
- **Contras:** Toque amplio, riesgo de romper mobile donde `py-4` da respiración interna necesaria; cada form tendría que compensar con padding propio inconsistente. No escala.

### C) Re-arquitecturar DialogContent a `flex flex-col` con secciones con scroll separado
- Header fijo + body scrolleable + footer fijo, con `overflow` controlado.
- **Pros:** Control fino de áreas.
- **Contras:** Invasivo, cambia comportamiento de todos los diálogos, necesita QA exhaustivo, over-engineering para un gap.

**Recomendación:** Opción A. Es el cambio ya preparado en el diff y coincide con la intención del diseño mobile-native (bottom-sheet ya quería `gap-0` para que el handle y el contenido no tengan doble separación).

## 4) Diseño aprobado (secciones)

### 4.1 Arquitectura
- **Single source:** `src/apps/Budgie.Web/src/components/ui/dialog.tsx:40` — `DialogContent`.
- No se introducen nuevos componentes ni wrappers. `Sheet` = `DialogPrimitive.Popup` con clases responsivas.
- Los formularios mantienen `className="grid gap-4 py-4"` (o equivalente) como padding interno de contenido; el Popup aporta `p-4` exterior y `gap-0` para que header→body sea solo `py-4` (16px).

### 4.2 Componentes afectados
- `[MODIFY] src/apps/Budgie.Web/src/components/ui/dialog.tsx` — Popup classes.
- **Verificación sin cambios:** `TransactionFormDialog`, `IncomeExpenseForm`, `TransferForm`, `LoanForm`, `CollectionPaymentForm`, `AccountFormDialog`, `CategoryFormDialog`, `GoalFormDialog`, `PersonFormDialog` — se listan como superficies de regression visual, no se modifican.

### 4.3 Clases exactas (contrato)
```tsx
// Antes (HEAD bug):
"fixed inset-x-0 bottom-0 ... gap-4 ... p-4 ... sm:pb-4 ..."
// Después (fix):
"fixed inset-x-0 bottom-0 z-50 grid max-h-[90dvh] w-full gap-0 overflow-y-auto ... p-4 ... sm:gap-0 sm:p-4"
```
- Mobile: `gap-0` (no doble gap), `p-4`, `pb-safe`, `rounded-t-2xl`, `max-h-[90dvh]`
- Desktop: `sm:gap-0 sm:p-4 sm:rounded-xl sm:overflow-visible` + animaciones `zoom-in` existentes.

### 4.4 Flujo de datos / render
Sin cambios lógicos. Solo CSS. No afecta `onOpenChange`, focus trap de Base UI, ni `DialogOverlay`.

### 4.5 Manejo de errores
N/A (cambio visual). Si el archivo no existe en CI, el test de regresión falla con mensaje explícito.

### 4.6 Testing
- **Regression guard existente:** `tests/Budgie.Modules.Finances.UnitTests/Infrastructure/DialogLayoutRegressionTests.cs:30` — aserta `gap-0` presente y `w-full gap-4 overflow` ausente; también cubre `TransactionRepository` y `CreateTransactionCommandHandler` (alcance del diff colateral, no de este fix pero ya en el working-tree).
- **Frontend:** `bun run typecheck`, `bun run build`, `bun run lint` verdes. Verificación visual manual: abrir cualquier modal en viewport `>= 640px` y comprobar que el primer field está a 16px del header.
- No se requieren nuevos tests E2E (el bug es puramente gap CSS); si se desea, Playwright snapshot de `DialogContent` en desktop.

### 4.7 No-funcionales
- Performance: 0 impacto.
- A11y: sin cambio.
- i18n: sin cambio.

## 5) Fuera de alcance
- Backend (`TransactionRepository`, `CreateTransactionCommandHandler`) y cambios de fecha/cuotas en working-tree — pertenecen a otra spec (Money Manager import / installments).
- Cambios de diseño adicionales en mobile bottom-sheet.
- Nuevos componentes `ResponsiveSheet`.

## 6) Riesgos y mitigación
- **Riesgo:** Reintroducir `gap-4` en futuro refactor → mitigado por `DialogLayoutRegressionTests`.
- **Riesgo:** Formularios con `py-4` duplicado + nuevo `p-4` del Popup generen doble padding lateral → no, porque Popup `p-4` es exterior y form `py-4` es vertical interno; horizontal es `p-4` del Popup ya existente.

## 7) Criterio de done
- `DialogContent` en `dialog.tsx` contiene `gap-0` y `sm:gap-0 sm:p-4`, no contiene `w-full gap-4 overflow`.
- `DialogLayoutRegressionTests.DialogContent_ShouldNotHaveGap4_ShouldUseGap0` en verde.
- Build web (`tsc`, `vite build`) verde en desktop viewport.

## 8) Trazabilidad
- Spec: `docs/superpowers/specs/2026-09-09-modal-forms-offset-fix-design.md`
- Plan: `docs/plans/02-modal-forms-offset-fix.md` (siguiente fase)
- Código: `src/apps/Budgie.Web/src/components/ui/dialog.tsx`
