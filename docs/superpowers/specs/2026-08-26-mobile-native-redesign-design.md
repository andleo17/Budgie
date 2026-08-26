# Rediseño móvil tipo app nativa — Diseño

**Fecha:** 2026-08-26
**Estado:** Aprobado por el usuario (pendiente revisión de spec)

## Objetivo

Compactar el resumen informativo de Transacciones y dar a todas las páginas
un aspecto y comportamiento de app nativa moderna (estilo Revolut) en móviles,
usando componentes shadcn/ui existentes y sin cambios de arquitectura ni rutas.

## Decisiones previas (con el usuario)

1. **Alcance:** global — todas las páginas principales.
2. **Resumen:** fila única de 3 métricas (~40px); saldo final solo en vista de cuenta única.
3. **Navegación:** se mantiene el sidebar/hamburguesa actual (sin tab bar).
4. **Referencia visual:** app moderna tipo Revolut (tarjetas redondeadas, aire, iconos coloridos).
5. **Diálogos en móvil:** bottom sheets.
6. **Estrategia:** Opción A — restyle responsivo en sitio con primitivas compartidas.

## Arquitectura

### Primitivas compartidas (`src/components/shared/`)

#### `MetricStrip.tsx`
- Reemplaza a `TransactionSummaryBar`.
- Fila única compacta (~40px de alto): 3 métricas → **Ingresos** (verde),
  **Gastos** (rojo), **Neto** (semántico según signo), separadas por
  `Separator` vertical de shadcn.
- Montos con `tabular-nums`.
- Props compatibles con los datos actuales: `income`, `expenses`, `net`
  (moneda principal) + desglose multi-moneda opcional en subtexto de altura
  fija (o tooltip si no cabe).
- **Saldo final:** 4ª métrica condicional; solo en vista de cuenta única.
  Nunca altera la grilla (fila única siempre; 4 columnas iguales cuando existe).
- Skeleton propio coherente con la altura final (~40px).

#### Bottom sheets (implementación en `ui/dialog.tsx`)
> Desviación del diseño original: en lugar de un wrapper `ResponsiveSheet`,
> el comportamiento bottom-sheet vive directamente en `DialogContent` de
> shadcn. Como `Sheet` es el mismo primitivo Dialog de base-ui, se logró con
> CSS responsivo dentro del componente: **todos** los diálogos existentes
> heredan el patrón sin migrar llamadores, evitando dualidad de patrones.
- `< sm`: sheet inferior full-width, animación slide-up, handle visual,
  `rounded-t-2xl`, altura máxima `90dvh` con scroll interno,
  `overscroll-behavior: contain` y safe-area iOS.
- `≥ sm`: diálogo centrado actual (sin cambios visuales ni de animación).

#### Tokens Revolut (`src/index.css`)
- Radio de tarjetas: `rounded-2xl` (utilidad compartida).
- Sombras suaves de elevación donde aporten (sin bordes duros innecesarios).
- Utilidades `.amount-positive` / `.amount-negative` para montos.

## Migración por página (orden de implementación)

1. **Transacciones**
   - Resumen → `MetricStrip`.
   - Lista: filas con icono circular colorido por categoría (`category.color`),
     monto derecho `tabular-nums`, nota como segunda línea, headers sticky
     compactos.
   - Touch targets ≥44px por fila; feedback táctil `active:bg-muted`
     (hover solo en desktop).
   - Tarjeta de saldo de cuenta única: desaparece como tarjeta independiente;
     su saldo pasa a ser la 4ª métrica del strip.
2. **Dashboard**: stats apiladas en móvil; gráfico de spending con altura fija reservada.
3. **Cuentas / Personas / Categorías / Metas / Templates**:
   tarjetas full-width en móvil, jerarquía clara, acciones en menú existente.
4. **Formularios**: bottom sheets automáticos vía `DialogContent` (ver
   primitivas) — aplicado a todos los diálogos existentes sin cambios por
   formulario. Los diálogos de edición de solo lectura (transfer/collection)
   incluidos.

## Detalles nativos transversales

- `-webkit-tap-highlight-color: transparent` global.
- `overscroll-behavior: contain` en sheets; momentum scroll en listas.
- Safe areas iOS: `env(safe-area-inset-bottom)` aplicado en sheets y contenido inferior.
- Skeleton existentes ajustados a las nuevas formas (el trabajo previo del
  summary bar y tarjeta de saldo se adapta al `MetricStrip`).

## Convenciones técnicas

- Componentes shadcn/ui exclusivamente (`Sheet`, `Separator`, `Skeleton`,
  `Button`, etc.) + Tailwind responsive (`sm:` como corte móvil/desktop).
- Sin dependencias externas nuevas (nada tipo Framer Motion; animaciones vía
  Tailwind/CSS y data-state de shadcn).
- i18n: reutilizar claves existentes; claves nuevas mínimas documentadas en
  `en.ts`/`es.ts`.
- Verificación: build/tsc/eslint limpios + revisión manual por página. No hay
  suite E2E UI en el repo; no se añade infra nueva (YAGNI).

## Fuera de alcance

- Sidebar/navegación, backend/API, tab bar inferior,
  design system completo (Opción C).

## Riesgos

- Divergencia visual entre páginas → mitigado con primitivas compartidas.
- Crecimiento dinámico del MetricStrip por desglose multi-moneda → subtexto de
  altura fija o tooltip; la fila nunca cambia de alto sin espacio reservado.
