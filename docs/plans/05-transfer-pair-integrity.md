# Implementation Plan — Transfer pair integrity

- **Design spec:** `docs/superpowers/specs/2026-10-12-transfer-pair-integrity-design.md`
- **Commit:** `c616eae`
- **Module:** `Budgie.Modules.Finances`

## User Review Required

> [!NOTE]
> Decisiones de spec ya integradas:
> - Se mantiene `LinkedTransactionId` como id de grupo compartido (sin migración de datos).
> - La comisión **no** es editable vía `UpdateTransactionCommand` (no existe campo `Fee`); la pata de comisión se deja intacta.
> - `UpdateAsync` del repositorio gana la capacidad de cambiar `AccountId`.
> - El endpoint de transferencia devuelve el `Id` de la pata saliente en lugar del groupId (el frontend ignora el cuerpo).

## Proposed Changes

### 1. Repositorio: acceso al grupo (`Budgie.Modules.Finances/Application` + `Infrastructure/Database`)

- **Objetivo**: poder cargar todas las patas de un grupo y poder mover una pata de cuenta.
- **Archivos**:
  - `[MODIFY] src/core/Budgie.Modules.Finances/Application/ITransactionRepository.cs`
  - `[MODIFY] src/core/Budgie.Modules.Finances/Infrastructure/Database/TransactionRepository.cs`
- Pasos:
  - Añadir a la interfaz: `Task<List<TransactionForUpdate>> GetTransferGroupAsync(Guid groupId, Guid userId, CancellationToken ct);`
  - Implementarla con un `SELECT` por `LinkedTransactionId = @GroupId AND UserId = @UserId`, reutilizando el mapeo de `TxUpdateRow` ya existente en `GetByIdForUpdateAsync`.
  - Añadir `""AccountId"" = @AccountId` al `UPDATE` de `UpdateAsync` y `AccountId = tx.AccountId` a los parámetros. Es el único cambio de esquema de escritura.

### 2. Lectura: destino determinista (`Infrastructure/Database`)

- **Objetivo**: que el destino mostrado sea la otra pata, no una arbitraria.
- **Archivos**:
  - `[MODIFY] src/core/Budgie.Modules.Finances/Infrastructure/Database/TransactionRepository.cs`
  - `[MODIFY] src/core/Budgie.Modules.Finances/Application/Transactions/TransactionResponse.cs` (solo si hace falta documentar el campo)
- Pasos:
  - En `BuildResponsesAsync`, ampliar el `SELECT` de `destRows` con `t.""Id""`.
  - Añadir `Id` al `record AccountNameRow`.
  - Sustituir `g => g.First()` por una agrupación en lista y elegir `candidates.FirstOrDefault(c => c.Id != item.Id)`; si no hay candidato distinto, dejar `destId`/`destName` a `null`.
  - No cambia el contrato HTTP.

### 3. Creación idempotente (`Application/Transactions/Transfer`)

- **Objetivo**: que reintentar una importación no duplique transferencias.
- **Archivos**:
  - `[MODIFY] src/core/Budgie.Modules.Finances/Application/Transactions/Transfer/TransferCommand.cs`
  - `[MODIFY] src/core/Budgie.Modules.Finances/Application/Transactions/Transfer/TransferCommandHandler.cs`
- Pasos:
  - `TransferCommand` gana `string? ExternalUid = null` al final del record.
  - En el handler, antes de crear nada: si `ExternalUid` no es nulo, consultar `GetIdByExternalUidAsync`; si devuelve un id, retornarlo sin escribir.
  - Propagar `ExternalUid` a las patas creadas (campo ya existe en `Transaction` y en el `INSERT`).
  - Devolver `outgoing.Id` en lugar de `transferId`.

### 4. Actualización como unidad (`Application/Transactions/UpdateTransaction`)

- **Objetivo**: editar una transferencia modificando ambas patas y los saldos.
- **Archivos**:
  - `[MODIFY] src/core/Budgie.Modules.Finances/Application/Transactions/UpdateTransaction/UpdateTransactionCommandHandler.cs`
- Pasos:
  - Nada más cargar `existing`, si `existing.Type == Transfer` delegar en un método privado `UpdateTransferAsync` y retornar su resultado.
  - `UpdateTransferAsync`:
    - Cargar el grupo con `GetTransferGroupAsync(existing.LinkedTransactionId!.Value, userId, ct)`.
    - Validar: `request.Type == TransactionType.Transfer`; `request.Installments == 1`; grupo con exactamente una pata `Transfer` y una `Income` (en otro orden, error `Transfer.MalformedGroup`).
    - Revertir el impacto antiguo en la cuenta origen y en la destino anterior.
    - Aplicar `UpdateAsync` a la pata saliente y a la de entrada con el nuevo monto, moneda, tipo de cambio, fecha y notas, conservando `LinkedTransactionId` y `CategoryId` de sistema.
    - Si la cuenta destino cambia, fijar `AccountId` de la pata de entrada a la nueva y aplicar el impacto allí (revirtiendo en la anterior).
    - La pata de comisión, si existe, se deja intacta.
  - Los errores nuevos usan códigos `Transfer.*` con `Error.Validation`, coherente con el resto del handler.

### 5. Borrado como unidad (`Application/Transactions/DeleteTransaction`)

- **Objetivo**: borrar una transferencia borra sus patas y revierte todos los saldos.
- **Archivos**:
  - `[MODIFY] src/core/Budgie.Modules.Finances/Application/Transactions/DeleteTransaction/DeleteTransactionCommandHandler.cs`
- Pasos:
  - Tras obtener la transacción, si `tx.Type == Transfer` y tiene `LinkedTransactionId`, cargar el grupo y borrar cada pata revirtiendo el impacto en cada cuenta distinta implicada.
  - Reutilitar `RevertTransaction`/`DeleteTransactionAsync` ya usados para los hijos; no se toca la ruta de hijos por `ParentTransactionId`.

### 6. Tests unitarios (`tests/Budgie.Modules.Finances.UnitTests`)

- **Objetivo**: cubrir los caminos nuevos al ~100%.
- **Archivos**:
  - `[NEW] tests/Budgie.Modules.Finances.UnitTests/Application/Transactions/TransferPair/TransferPairIntegrityTests.cs`
  - `[NEW] tests/Budgie.Modules.Finances.UnitTests/Infrastructure/TransferDestinationResolutionTests.cs`
- Pasos:
  - Actualización: propaga monto/fecha/notas a ambas patas y conserva el enlace; mueve la pata de entrada al cambiar destino (ajustando ambos saldos); mueve la saliente al cambiar origen; rechaza tipo distinto de `Transfer`; rechaza `Installments > 1`; rechaza grupo mal formado.
  - Borrado: elimina todas las patas del grupo y revierte cada cuenta implicada.
  - Creación: con `ExternalUid` existente no escribe y devuelve el id previo.
  - Resolución de destino: con grupo de dos patas elige la otra; con grupo de una sola pata devuelve `null`.

### 7. Tests de integración (`tests/Budgie.IntegrationTests`)

- **Objetivo**: verificar los caminos contra PostgreSQL real.
- **Archivos**:
  - `[NEW] tests/Budgie.IntegrationTests/TransferPairIntegrityTests.cs`
- Pasos:
  - `POST /api/finances/transactions/transfer` dos veces con el mismo `ExternalUid` → una sola transferencia.
  - `PUT /api/finances/transactions/{id}` cambiando el destino → ambas patas y ambos saldos correctos.
  - `DELETE /api/finances/transactions/{id}` sobre una pata → el grupo completo desaparece y los saldos quedan consistentes.
  - `GET /api/finances/transactions` → la pata `Transfer` muestra el destino correcto.

## Verification Plan

- `dotnet build Budgie.slnx`
- `dotnet test Budgie.slnx`
- `cd src/apps/Budgie.Web && bun run build` (no hay cambios de frontend; se ejecuta para confirmar que no rompe)
