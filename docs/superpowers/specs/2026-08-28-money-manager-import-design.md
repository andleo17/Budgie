# Importación Money Manager (.mmbak) — Diseño

**Fecha:** 2026-08-28
**Estado:** Borrador — pendiente aprobación Fase 1
**Entrada:** `implementa la importación del archivo @.opencode/docs/MMGF(28-08-26-010218).mmbak que es una base de datos de una app llamada "money manager" de la Google Play Store, analiza el archivo y fija la estrategia para la importación.`
**Archivo analizado:** `.opencode/docs/MMGF(28-08-26-010218).mmbak` — 1,3 MB, SQLite 3.x (user_version=19, 317 páginas, 4546 file counter)

---

## 1. Resumen ejecutivo

Se importa el backup de **Money Manager (Realbyte Inc.)** — app Android que guarda su DB como `.mmbak` que es **SQLite 3.x sin cifrar** (renombrado). Contiene 2294 movimientos reales + cuentas/categorías/presupuestos/recurrencias. La estrategia fija es: **endpoint de upload + dry-run de previsualización + commit transaccional** dentro del módulo `Finances`, mapeando el esquema MM a las entidades Budgie existentes (Accounts, Categories, Transactions, RecurringTransactions), con deduplicación de transfers y validación por `UserId` multi-tenant. Sin cambios de arquitectura.

## 2. Contexto y descubrimiento (Fase 0)

### 2.1 Proyecto detectado

| Elemento | Valor descubierto |
|---|---|
| Solución | `Budgie.slnx` |
| Estructura | `src/core/Budgie.Shared`, `Budgie.Modules.Users/Finances/Relationships` + `src/apps/Budgie.Api` + `src/apps/Budgie.Web` |
| Tests | `tests/Budgie.IntegrationTests`, `Budgie.Modules.{Finances,Relationships,Users}.UnitTests` |
| Frontend | `src/apps/Budgie.Web` (React 19 + Vite + shadcn/ui + TanStack Query) — **sí aplica E2E** |
| SDD Workflow | `AGENTS.md` vigente (Brainstorming→Writing Plans→Implementing→Testing→Review→Verifying→Release), hard-gate spec+plan |
| Convenciones | Clean Architecture + Dapper (SQL solo en Infrastructure), CQRS `Mediator+ErrorOr`, `AccountType/Category/Transaction` con `UserId` tenancy, Dapper `IDbConnectionFactory`, `Money/CurrencyCode/Name` value objects, `LoggerMessage` source-gen |
| Estado git | último commit `6460812 test(web): add playwright e2e`, working tree limpio salvo `.opencode/docs/` (untracked) |
| Specs previas | `docs/superpowers/specs/2026-08-26-mobile-native-redesign-design.md` |

### 2.2 Análisis del .mmbak (evidencia ejecutada)

Comandos verificados: `file`, `sqlite3 sqlite_master`, conteos por tabla.

**Esquema MM (19 tablas, 2 relevantes vacías):**

| Tabla | Filas | Rol |
|---|---|---|
| `CURRENCY` | 2 | PEN (main, rate 1.0), USD (rate 3.5665) |
| `ASSETGROUP` | 11 | Grupos de cuenta (1 Cuentas, 2 Tarj. crédito, 4 Ahorros, 11 Efectivo, etc.) |
| `ASSETS` | 20 | Cuentas reales (17 normales + 3 placeholder `11,Cuentas,2 Tarjetas...`). `uid` es clave lógica, `groupUid→ASSETGROUP`, `currencyUid→CURRENCY`, `ZDATA 0/1` discrimina placeholder vs real |
| `ZCATEGORY` | 92 | Categorías jerárquicas (`TYPE 0 income / 1 expense`, `pUid` padre, `C_IS_DEL`, `STATUS`). Ej: `6 🍜 Comida` → hijos `19 👩‍👦‍👦 Familiar` |
| `INOUTCOME` | 2294 | **Transacciones** — clave del import |
| `TAG/TX_TAG` | 2 / 0 | Tags sistema (`Prepayment`, `Exception`), sin uso real |
| `BUDGET/BUDGET_AMOUNT` | 26 / 46 | Presupuestos por categoría (`targetUid`, `PERIOD_TYPE=6` mensual, `AMOUNT/BUDGET_PERIOD`) |
| `REPEATTRANSACTION` | 5 | Recurrencias (DO_TYPE + REPEAT_TYPE, NEXT_DATE epoch) |
| `FAVTRANSACTION/MEMO/PHOTO/ZETC/CODE_*` | ≤20 c/u | Favoritos, notas, adjuntos, settings — fuera de alcance inicial |
| `SMS_RAW_READ` | 0 en backup | No aplica |

**INOUTCOME — detalle crítico (validado con queries):**

```sql
SELECT DO_TYPE, COUNT(*) FROM INOUTCOME GROUP BY DO_TYPE
-- 0:369 | 1:1525 | 3:195 | 4:195 | 7:10
```

| DO_TYPE | Significado inferido | Evidencia | Mapeo Budgie |
|---|---|---|---|
| `0` | **Income** (ingreso) | `ctgUid` apunta a categorías TYPE 0 (`💵 Dinero extra`, `💰 Salario`, `🤝 Devolución`), `ZCONTENT` con pagador, `toAssetUid='-4'` | `TransactionType.Income` |
| `1` | **Expense** (gasto) | 1525 filas (66%), categorías TYPE 1 (`🍜 Comida`, `🤝 Favores`, `Para mí`), `toAssetUid` numérico `-4/22/28` | `TransactionType.Expense` |
| `3` | **Transfer-out** (salida) | 195, `assetUid→toAssetUid` ambas son `ASSETS.uid` válidos, `ZDATE/WDATE` | Mitad de `TransactionType.Transfer` |
| `4` | **Transfer-in** (entrada espejo) | 195, **par exacto de DO_TYPE 3**: mismo `ZMONEY`/`WDATE`, `assetUid/toAssetUid` invertidos (ver AID 66↔67, 68↔69) | Duplicado — **deduplicar, importar solo una** |
| `7` | **Initial balance** (saldo inicial) | 10, `ctgUid='-4'`, `ZCONTENT='Ingresos'`, montos como 1122.67, -82000, 2069.07 | `Transaction.IsInitialBalance=true` + set `Account.Balance` |

Campos por fila: `uid (PK lógico)`, `assetUid`, `toAssetUid` (`-4` = sin destino vs UID real para transfer), `ctgUid` (`-4` = sin categoría), `ZDATE` (epoch ms string, ej `1739886045689`), `WDATE` (`YYYY-MM-DD`), `ZMONEY` (string monto), `IN_ZMONEY`, `AMOUNT_ACCOUNT` (real convertido), `currencyUid` (`PEN_PEN`/`PEN_USD`), `ZCONTENT` (nota), `IS_DEL=0` siempre en backup, `MARK`, `lat/lng/gstd` vacíos.

Rangos: `WDATE 2025-01-24 .. 2028-03-12` (incluye recurrencias futuras), `IS_DEL=0` en 2294 (sin borrados lógicos), `currencyUid` 2162 PEN / 132 USD, `AMOUNT_ACCOUNT` ya convertido.

**Otros hallazgos:**

- Cuentas reales (ejemplos): `BCP Soles PEN_PEN group 1 Cuentas`, `BCP Dólares PEN_USD`, `BCP AMEX group 2 Tarjetas`, `Wardaditos grupo 4 Ahorros`, `Casa Calamardo grupo 5 Préstamo`. Tipo Budgie derivado de `ASSETGROUP.TYPE`: 1→Checking, 2→Credit, 4→Savings, 5→Debt, 11→Cash. `A_UTIME` epoch ms es creación.
- Categorías con jerarquía pUid (ej: `6 🍜 Comida` padre de `19 Familiar`). `C_IS_DEL=1` marca borradas (ej `🖼 Cultura`, `💄 Productos de belleza`). `STATUS` 0/2.
- `BUDGET.uid` formato `"{targetUid}_1_0_0_6"` (targetUid + DO_TYPE + PERIOD_TYPE). `BUDGET_AMOUNT.budgetUid` FK, `IS_DEL` en algunos.
- `REPEATTRANSACTION` con `REPEAT_TYPE 1,8,16` (daily/weekly/monthly/yearly guess) y `NEXT_DATE` futuro.

## 3. Objetivo y criterios de éxito

### Objetivo

Permitir a un usuario Budgie **importar su backup `.mmbak` de Money Manager** en una acción, creando sus cuentas, categorías (con jerarquía), transacciones (incluyendo transfers deduplicados y saldos iniciales), y recurrentes/presupuestos donde mapee, sin duplicar datos en re-importes y sin afectar a otros usuarios.

### Criterios de éxito (verificables)

1. Usuario autenticado sube `.mmbak` → ve **preview/dry-run** con conteos por entidad y warnings (cuentas 17→?, categorías 92→~80 tras filtros, transacciones 2294→~2099 tras dedup transfer+excluir placeholders) y errores bloqueantes.
2. Tras confirmar, se crean: `Accounts` con `Currency`/`Type` correctos y `ControlStartDate` basado en primera TX; `Categories` con padres preservados; `Transactions` con `Type/Amount/Currency/ExchangeRate/Date/Notes/CategoryId` y `Transfer` con 1 fila por par 3/4 + balance final consistente.
3. Re-import del mismo archivo es **idempotente** (por `uid` externo almacenado o hash) — no duplica.
4. Validaciones: archivo no-sqlite, tablas faltantes, monedas desconocidas, categorías huérfanas, montos inválidos → `Error.Validation` claro (i18n es/en).
5. Multi-tenancy: todo `WHERE UserId = @UserId`.
6. Gates verdes: `dotnet build`, `dotnet test`, `bun run typecheck/lint/build`, Playwright si toca UI.

## 4. Alcance

### Dentro

- Parser `.mmbak` (SQLite) en backend: validación, lectura de `CURRENCY/ASSETGROUP/ASSETS/ZCATEGORY/INOUTCOME/BUDGET(+AMOUNT)/REPEATTRANSACTION`.
- Mapeos a dominio Budgie: `CurrencyCode`, `Account (Type, Balance, CreditCardSettings mínimo)`, `Category (ParentId, MonthlyBudget desde BUDGET)`, `Transaction (Income/Expense/Transfer, IsInitialBalance)`, `RecurringTransaction` (si factible).
- Deduplicación transfers (par 3/4 → 1 Transfer), filtrado placeholders `ASSETS` (`ZDATA=1`), `IS_DEL=1`, `C_IS_DEL=1`.
- Import Module/Feature `ImportMoneyManager` con Commands `PreviewImport` + `CommitImport` (CQRS), Repositorio dedicado + tabla staging `ImportRuns` o `ExternalId` en `Transactions/Accounts/Categories` para idempotencia.
- Endpoint `POST /api/import/money-manager/preview` y `/commit` (multipart/form-data), auth required.
- UI mínima en `Budgie.Web` (página o modal en Settings): upload, preview table, confirm. Mockeada con MSW para E2E.
- Logging `LoggerMessage` source-gen y manejo `ErrorOr`.

### Fuera

- SMS parsing (`MESSAGEMACRO2`, `SMS_RAW_READ`), fotos (`PHOTO`), `MEMO`, `FAVTRANSACTION`, `ZETC`, `CODE_*`, `TAG/TX_TAG` (vacío), geolocalización, deprecación de saldo a `ExchangeRates`.
- Import incremental continuo / sync en background / Workers Build.
- Soporte genérico `.csv`/`.xlsx` u otros backups.
- Migración de deudas/personas divididas (`ZCONTENT` "Mamá 50/50" queda como nota; split engine futuro).

## 5. Enfoques evaluados (trade-offs y recomendación)

### Enfoque A — Backend puro sin preview (rápido)

Upload → parse+write directo en una transacción. Pros: mínimo código, 1 endpoint. Contras: sin visibilidad previa, errores a mitad dejan estado parcial si no es todo transaccional, re-import propenso a duplicados, mala UX para 2294 filas.

### Enfoque B — Backend dry-run + commit (recomendado) ✅

Dos fases: `PreviewImport` (solo lectura, sin writes, retorna summary JSON) y `CommitImport` (re-parse + writes transaccionales, soporta `dryRun` idempotente). Pros: UX segura (usuario ve conteos/warnings antes de mutar), validación completa sin tocar DB, idempotencia verificable, testeable. Contras: parsea dos veces (mitigable cacheando temp file hash), un poco más de código.

### Enfoque C — CLI offline + SQL script

Herramienta `dotnet run -- import mmbak.db` que genera `sql/002_import.sql`. Pros: sin upload, útil para migrador admin. Contras: no self-service, no multi-tenant, fuera del ciclo SDD web, requiere acceso DB, no cubre validación interactiva.

**Recomendación:** **B** (dry-run+commit). Cubre seguridad y UX con coste incremental bajo; A se queda corto para 2294 filas y deuda de idempotencia. Se deja CLI como evolución opcional (no bloqueante).

## 6. Diseño propuesto (B — detalle)

### 6.1 Arquitectura y rutas reales

```
src/core/Budgie.Modules.Finances/
  Application/Import/
    PreviewMoneyManagerImport/
      PreviewMoneyManagerImportCommand.cs      // record(IFormFile/FileBytes, UserId) : IRequest<ErrorOr<PreviewResult>>
      PreviewMoneyManagerImportCommandHandler.cs // depende de IMoneyManagerParser + I...Repository (solo lectura para validar)
    CommitMoneyManagerImport/
      CommitMoneyManagerImportCommand.cs       // record(FileBytes, Options: Overwrite?) : IRequest<ErrorOr<CommitResult>>
      CommitMoneyManagerImportCommandHandler.cs // transaccional, escribe via repos
    MoneyManagerParser.cs                     // interfaz + impl en Infrastructure (Infrastructure/Import/)
    MoneyManagerModels.cs                     // DTOs internos (MmCurrency, MmAsset, MmCategory, MmTransaction...)
    Mappings/MoneyManagerMappings.cs          // extensión pura (sin SQL)
  Infrastructure/Import/
    MoneyManagerSqliteParser.cs               // usa Microsoft.Data.Sqlite, abre .mmbak como SQLite
    ImportRunRepository.cs                    // opcional: tabla ImportRuns para idempotencia
  Domain/Import/ImportRun.cs                  // si se persiste run (opcional)
  Presentation/Import/
    PreviewImportEndpoint.cs
    CommitImportEndpoint.cs
    ImportEndpoints.cs                        // MapGroup /api/import/money-manager

src/apps/Budgie.Api/sql/002_import_runs.sql  // CREATE TABLE "ImportRuns" + columnas ExternalId en Accounts/Categories/Transactions si se elige
src/apps/Budgie.Web/src/features/import/     // página ImportPage.tsx + hooks usePreviewImport/useCommitImport + i18n es/en
tests/Budgie.Modules.Finances.UnitTests/Import/
tests/Budgie.IntegrationTests/Import/
```

Registro: `FinancesModule.AddFinancesModule()` añade `IMoneyManagerParser`; `FinancesModule.MapFinancesEndpoints()` mapea `MapImportEndpoints()`.

### 6.2 Mapeos exactos (tabla → entidad)

| Origen MM | Filtro | Destino Budgie | Reglas |
|---|---|---|---|
| `CURRENCY` | `IS_DEL=0` | `UserSettings.SupportedCurrencies` + validación `CurrencyCode` | `ISO` → `CurrencyCode` (PEN, USD). `RATE` solo para `ExchangeRate` estimado. `IS_MAIN_CURRENCY=1` → `MainCurrency` sugerido (no sobreescribe si ya configurado) |
| `ASSETGROUP.TYPE` | — | `AccountType` | `1 Cuentas→Checking(1)`, `2 Tarj.crédito→Credit(2)`, `3 Débito→Checking`, `4 Ahorros→Savings(1)`, `5 Préstamo→Debt(4)`, `6 Prepago→Checking`, `11 Efectivo→Cash(3)`, otros→Checking. Placeholder `ASSETS.ZDATA=1` se ignora (son contenedores `Cuentas/Tarjetas...`) |
| `ASSETS` | `ZDATA=0 AND NIC_NAME NOT LIKE 'Cuentas\|Tarjetas'` | `Account` | `uid→ExternalId`, `NIC_NAME→Name (100 chars)`, `currencyUid→Currency (ISO)`, `groupUid→Type`, `A_UTIME→CreatedAt/ControlStartDate` (mín de INOUTCOME por cuenta). `Credit` sin settings extra inicial (BillingDay null). Balance = sum de INOUTCOME. `IsTransExpense` ignorado |
| `ZCATEGORY` | `C_IS_DEL=0 AND STATUS!=?` (filtrar borradas) | `Category` | `uid→ExternalId`, `NAME→Name (50)`, `TYPE 0→1?` mapear a `Category.Type` (0 Income / 1 Expense en Budgie? Confirmar en plan; fallback: 0/1 directo), `pUid→ParentId` (resolver tras crear padres; dos pasadas), `Color/Icon` null, `MonthlyBudget` desde `BUDGET_AMOUNT` join `BUDGET.targetUid=ZCATEGORY.uid AND BUDGET_AMOUNT.IS_DEL=0` (si múltiple, tomar max) |
| `INOUTCOME` | `IS_DEL=0 AND DO_TYPE IN (0,1,3,7)` (4 deducido) | `Transaction` | Ver subsección. Ordenar por `WDATE ASC, ZDATE ASC` |
| `BUDGET_AMOUNT` | `IS_DEL=0` | `Category.MonthlyBudget` | No entidad Budget separada (Budgie no tiene tabla Budget); fallback a `MonthlyBudget` |
| `REPEATTRANSACTION` | `IS_DEL=0` | `RecurringTransaction` | `DO_TYPE→Type`, `REPEAT_TYPE 1 Daily/8 Monthly/16 Yearly?` → `RecurringFrequency`, `NEXT_DATE→NextDueDate`, `AMOUNT_SUB→Amount`, `currencyUid→Currency`. Si ambiguity, importar como `Frequency.Monthly` y marcar warning |

**INOUTCOME → Transaction detalle:**

- `uid` (22) → `ExternalId` (nuevo col `ExternalUid TEXT` en Transactions para idempotencia, UNIQUE per UserId).
- `assetUid` → `AccountId` via mapa `ASSETS.uid → Account.Id` (error si huérfano).
- `DO_TYPE`: `0→Income`, `1→Expense`, `7→Income IsInitialBalance=true`, `3→Transfer` (solo un lado del par; `toAssetUid` válido → `Transfer` con `LinkedTransactionId` null inicial; se ignora DO_TYPE 4).
- Deduplicación transfer: si `DO_TYPE=3` y existe fila espejo `DO_TYPE=4` misma `ZMONEY/WDATE/currencyUid` y `assetUid/toAssetUid` invertidos → importar **una** `Transfer` (tipo `Transfer`). Estrategia: agrupar por `(WDATE, ZMONEY, assetUid, toAssetUid)` y tomar solo DO_TYPE 3. Si `toAssetUid='-4'` en DO_TYPE 0/1/7 → no es transfer.
- `ctgUid` → `CategoryId` via mapa ZCATEGORY.uid; si `-4` o no encontrado → null (uncategorized). Log warning.
- `ZMONEY`/`AMOUNT_ACCOUNT`/`IN_ZMONEY`: usar `AMOUNT_ACCOUNT` (ya en moneda de cuenta) como `Amount` decimal positivo (`Money.FromPositive`). Si `AMOUNT_ACCOUNT` null/0 usar `ZMONEY`. `currencyUid` → `Currency` (de `CURRENCY.ISO`). Si diverge de `Account.Currency`, calcular `ExchangeRate = AMOUNT_ACCOUNT / ZMONEY`? En muestra `PEN_USD` con `ZMONEY 4.87 IN_ZMONEY 1.32` → rate ≈0.27; Budgie `ExchangeRate` se guarda; `Amount` en su `Currency`. Regla: `Currency = CURRENCY.ISO`, `Amount = AMOUNT_ACCOUNT` si `currencyUid` mismatch → usar `currencyUid ISO` con `AMOUNT_ACCOUNT` y `ExchangeRate` derivado; si no calculable → 1.
- `WDATE` (`2025-02-18`) → `Date` (DateTimeUnspecified). Si `WDATE` nulo → parse `ZDATE` epoch ms (`1739886045689` → DateTime). `wtime/paid` ignorados.
- `ZCONTENT` (ej "Paola: Cine", "Mamá: 50/50") → `Notes` (500 chars, trim).
- `MARK`/`SMS_*`/`lat/lng` → ignorados.
- Orden cronológico para `ApplyTransaction` de balances y para no violar `ControlStartDate`.
- `InterestRate/Installments` → defaults (1, 0).

### 6.3 Flujo de importación

1. **Upload:** `multipart/form-data` con campo `file` (.mmbak), max 10 MB, content-type `application/octet-stream`. Auth `RequireAuthorization()`.
2. **Validate file:** magia SQLite (`SQLite format 3\0`), `sqlite_master` contiene `INOUTCOME/ASSETS/ZCATEGORY`, tamaño>0. Si falla → `Error.Validation("Import.InvalidFile")`.
3. **Parse (temp file):** copiar a temp path, abrir `Microsoft.Data.Sqlite` readonly, `SELECT` por tabla. No muta backup.
4. **Preview (dry-run):** sin writes. Construye summary: `{ currencies:[{iso}], accounts:[{name,type,currency}], categories:[{name,type,parent}], transactions:{income,expense,transfer,initialBalance, total, dateRange}, budgets, recurring, warnings:[orphanCategory, placeholderSkipped, transferDeduped 195→195?], errors:[] }`. Retorna `ErrorOr<PreviewResult>`.
5. **Commit:** re-parse + transacción DB (`BEGIN` vía Dapper `ExecuteAsync` con `IDbConnectionFactory` + `IDbTransaction`). Pasadas: (a) upsert currencies en `UserSettings` (merge), (b) crear `Accounts` (reusa si `ExternalUid` existe), (c) crear `Categories` en 2 pasadas (padres primero), aplicar `MonthlyBudget`, (d) crear `Transactions` ordenadas (deduplicando transfers), (e) `RecurringTransactions`, (f) actualizar `Account.Balance` vía `ApplyTransaction` o recalcular y set `Balance`. Commit/rollback. Retorna `CommitResult { createdAccounts, skipped, ... }`.
6. **Idempotencia:** Opción A (simple): col `ExternalUid` en Accounts/Categories/Transactions (`VARCHAR UNIQUE` per User) + `ImportRuns` (Id, UserId, FileHash SHA256, CreatedAt). Si hash ya existe y `overwrite=false` → `Error.Conflict("Import.AlreadyImported")` con preview previo. Opción B (sin schema): solo `ExternalUid`. Elegir A si se acepta migración 002; si no, B sin tabla extra.
7. **Errores parciales:** si alguna `Account/Category` falla mapping → acumula warning y salta TX (no aborta todo); si archivo corrupto → aborta.

### 6.4 Validaciones y seguridad

- Auth: `ICurrentUserProvider.GetUserId()` en handlers; ningún SQL sin `UserId`.
- Archivo: extensión `.mmbak/.db/.sqlite` permitida, MIME check, tamaño, virus scan no requerido.
- Límites: máx 5000 transacciones, 100 cuentas, 200 categorías (error si excede).
- Sanitización: `Name.From` valida longitud; `Money.FromPositive` valida monto; fechas fuera de rango 2000-2100 → warning.
- Logging: `LogMessages.MoneyManagerPreviewStarted/Completed`, `CommitStarted/AppliedCounts` via `[LoggerMessage]`.

### 6.5 Frontend (solo si se aprueba UI)

Ruta `src/apps/Budgie.Web/src/features/import/`: `ImportPage.tsx` en `/import` (o tab en Settings), `ImportUpload.tsx` (dropzone + file input), `ImportPreview.tsx` (tables de counts), `useImport.ts` (TanStack Query mutations para preview/commit). i18n `import.*` en `es.ts`/`en.ts`. Componentes shadcn (`Card`, `Table`, `Alert`, `Button`, `Skeleton`). MSW handler para Playwright E2E (mock preview/commit). Sin afectar sidebar/routing existente.

### 6.6 Alternativas de almacenamiento

- Temp file en servidor (`Path.GetTempFileName`) borrado en `finally`.
- No se persiste `.mmbak` original (GDPR). Opcional S3/R2 si se pide historial.

## 7. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| `DO_TYPE` mapping erróneo (app cambia codes) | Query de validación en preview muestra distribución; tests con backup real; fallback a `Expense` con warning |
| `WDATE` futuro (2028) por recurrencias mal modeladas | Importa pero marca warning `futureDate`; UI filtra con `ControlStartDate` |
| Categorías con emoji (🤑💰🍜) truncadas | `Name` permite unicode, length 50, no ascii-only; test con emojis |
| Moneda `RATE` no usada y ExchangeRate mal calculado | Usa `AMOUNT_ACCOUNT` como fuente; si rate incoherente → 1 y warning |
| Perf 2294 TX en una TX larga | Batch inserts (500 por batch) con Dapper, `UNNEST` o multi-`ExecuteAsync`; progress no requerido bloqueante |
| Idempotencia sin hash → duplicados en re-upload | Guardar `FileHash` en `ImportRuns` + `ExternalUid` en hijos |
| AOT / SQLite nativo en publish | `Microsoft.Data.Sqlite` con `SQLitePCLRaw.bundle_e_sqlite3` es AOT-safe; no `System.Data.SQLite` |

## 8. Preguntas para el usuario (bloquean plan si no se responden)

1. **¿Importar presupuestos como `Category.MonthlyBudget` o ignorarlos?** Propuesta: sí, mapear (46 amounts). Si presupuesto multi-período, tomar 0 (monthly).
2. **¿Importar `REPEATTRANSACTION` como `RecurringTransaction` o diferir?** Propuesta: sí (5 filas), mapear conservadoramente.
3. **¿Sobrescribir si la cuenta/categoría ya existe con mismo nombre?** Propuesta: matching por `ExternalUid` (no por nombre); si nombre colisiona pero uid distinto → crear con sufijo ` (imported)`.
4. **¿Página dedicada `/import` o sección en Settings?** Propuesta: `/import` accesible desde Settings + nav (no rompe IA existente).
5. **¿Persistir hash de archivo para bloquear re-import o permitir re-import con skip?** Propuesta: bloquear por defecto con opción `overwrite=true`.

## 9. Plan de verificación

- **Backend:** `dotnet build Budgie.slnx`, `dotnet test` (unit: parser, mappings, dedup, handler validation; integration: upload real backup a DB de test, asserts counts).
- **Frontend (si UI):** `bun run typecheck`, `bun run lint`, `bun run build`, `bunx playwright test --project=import` contra MSW.
- **Manual:** subir `.opencode/docs/MMGF(28-08-26-010218).mmbak` en local/dev y comparar balances con app original (spot-check 10 TX).

## 10. Trazabilidad inicial (sujeta a plan-writer)

| Slice | Archivos (estimado) |
|---|---|
| Parser + mappings | `[NEW] src/core/Budgie.Modules.Finances/Infrastructure/Import/MoneyManagerSqliteParser.cs`, `[NEW] Application/Import/MoneyManagerModels.cs` |
| Preview/Commit handlers | `[NEW] Application/Import/PreviewMoneyManagerImport/*`, `[NEW] Application/Import/CommitMoneyManagerImport/*` |
| Schema idempotencia | `[NEW] src/apps/Budgie.Api/sql/002_import_runs.sql`, `[MODIFY] src/apps/Budgie.Api/Program.cs` (aplicar script) |
| Endpoints | `[NEW] Presentation/Import/*`, `[MODIFY] FinancesModule.cs` |
| Frontend | `[NEW] src/apps/Budgie.Web/src/features/import/*`, `[MODIFY] src/apps/Budgie.Web/src/app/router.tsx`, `[MODIFY] locales` |
| Tests | `[NEW] tests/.../Import/*` |

## 11. Self-review

- Placeholders: ninguno (`.../Archivo` evitado, rutas reales).
- Contradicciones: no.
- Ambigüedad: `DO_TYPE→TransactionType` inferido pero validado con datos reales (369/1525/195 par/10); se documenta fallback.
- Scope: respeta Clean Architecture (SQL solo Infrastructure), tenancy, logging source-gen, AOT.

---

**Gate Fase 1:** Esperando **Aprobación Fase 1** del usuario. Tras aprobar, se delega en `plan-writer` (writing-plans) para plan detallado. Responde “Aprobar Fase 1” o comenta ajustes (ej. respuestas a §8).
