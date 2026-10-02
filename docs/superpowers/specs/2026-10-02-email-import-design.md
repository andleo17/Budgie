# Design: Importación de transacciones por correo

- **Fecha**: 2026-10-02
- **Módulo nuevo**: `Budgie.Modules.EmailImport`
- **Estado**: aprobado

## 1. Objetivo

Registrar automáticamente las transacciones de las cuentas de Budgie a partir de los correos
de notificación que envían los bancos. El sistema lee el buzón de correo del usuario, extrae
importe, fecha, comercio y dirección del movimiento, y crea la transacción en la cuenta que
corresponda.

Las decisiones que fijan el alcance inicial:

| Decisión | Valor |
|---|---|
| Recepción | Polling contra la API del proveedor, desde un `BackgroundService` en `Budgie.Api` |
| Conexión | Por usuario, OAuth individual |
| Multi-proveedor | La capa Application no menciona Gmail; el proveedor es una implementación de Infrastructure |
| Resolución de cuenta | Cada regla declara una cuenta fija |
| Reglas | Adaptadores de banco en código + reglas `generic.regex` configurables por el usuario |
| Confirmación | Auto-commit si la confianza supera el umbral; el resto va a una bandeja de revisión |
| v1 | Solo `generic.regex` + probador de reglas. Sin adaptadores `bank.*` |

## 2. Arquitectura

### 2.1 Por qué un módulo nuevo

El dominio de la importación de correo (conexiones, reglas, borradores) es distinto del dominio
de finanzas, aunque termine creando transacciones. Un módulo propio mantiene el límite limpio y
sigue la estructura de Clean Architecture de AGENTS.md sin excepciones.

`EmailImport` depende de `Finances` **solo a través de interfaces públicas de Application**
(`IAccountRepository`, `ICategoryRepository`) y del dispatcher de Mediator. Nunca comparte SQL.

### 2.2 Estructura

```
Budgie.Modules.EmailImport/
  EmailImportModule.cs                       # AddEmailImportModule() + MapEmailImportEndpoints()
  Domain/
    Connections/EmailConnection.cs
    Rules/EmailImportRule.cs
    Staging/PendingEmailTransaction.cs
    Parsing/ExtractedTransaction.cs
    Parsing/EmailParseContext.cs
    Parsing/EmailMessage.cs               # neutro: asunto, remitente, cuerpo, fecha, messageId
  Application/
    Connections/EmailAuthorizationUri.cs
    Connections/EmailCredentials.cs
  Application/
    Connections/                             # GetAuthorizationUri, CompleteConnection, DisconnectEmail, GetConnectionStatus
    Rules/                                   # GetRules, CreateRule, UpdateRule, DeleteRule, PreviewRule
    Inbox/                                   # GetPending, ApprovePending, DismissPending, SyncNow
    Abstractions/IEmailProviderClient.cs
    Abstractions/ISecretProtector.cs
    Abstractions/IMessageTextExtractor.cs   # puerto: mensaje → texto plano
    Parsing/IBankAdapter.cs
    Parsing/EmailParser.cs
    Parsing/Adapters/GenericRegexAdapter.cs
    Parsing/ConfidenceScorer.cs
    Parsing/ConfidenceWeights.cs
    Parsing/Normalization/AmountNormalizer.cs
    Parsing/Normalization/DateNormalizer.cs
    Repositories.cs                          # interfaces
  Infrastructure/
    Email/
      Gmail/GmailEmailProvider.cs
      Gmail/GmailMessageMapper.cs
    Security/AesSecretProtector.cs
    Parsing/RawEmailReader.cs               # MIME → cuerpo
    Parsing/HtmlToTextConverter.cs           # cuerpo → texto plano
    Database/                                # implementaciones Dapper
    Worker/EmailSyncWorker.cs                # BackgroundService (timer)
    Logging/LogMessages.cs
  Presentation/
    Connections/                             # endpoints OAuth
    Rules/
    Inbox/
```

### 2.3 Neutralidad de proveedor

La razón de ser de esta capa es que Gmail no será el único proveedor. Por eso ningún nombre de
Application, Domain o Presentation contiene "Gmail". El vocabulario es "conexión", "mensaje",
"credenciales" y "proveedor".

El puerto es `IEmailProviderClient`:

```csharp
public interface IEmailProviderClient
{
    EmailProvider Provider { get; }
    Task<EmailAuthorizationUri> BuildAuthorizationUriAsync(EmailConnection connection, CancellationToken ct);
    Task<EmailCredentials> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct);
    Task<EmailCredentials> RefreshAsync(string encryptedCredentials, CancellationToken ct);
    Task<IReadOnlyList<EmailMessage>> ListNewAsync(EmailConnection connection, CancellationToken ct);
    Task<EmailMessage> GetAsync(EmailConnection connection, string messageId, CancellationToken ct);
}
```

`EmailConnection.Provider` discrimina el registro de `IEmailProviderClient`, y `SyncCursor` es un
string opaco cuyo significado conoce únicamente el proveedor. Añadir Outlook, Microsoft Graph o
IMAP consiste en registrar otra implementación: **ni Application, ni Domain, ni el esquema cambian.**

Añadir un miembro a `EmailProvider` es aditivo y no requiere migración.

### 2.4 Pipeline

```
EmailSyncWorker (BackgroundService, timer)
  └─ IEmailSyncService.SyncAllAsync()                    ← toda la lógica, sin dependencia del reloj
       └─ IEmailImportRepository.GetEnabledConnectionsAsync()
            └─ providerClientFor(connection).ListNewAsync(connection)
                 └─ IEmailParser.Parse(message, rules)
                      ├─ confianza ≥ umbral  → staging + CreateTransactionCommand
                      └─ confianza < umbral  → staging (bandeja de revisión)
```

Dos restricciones sobre el worker:

1. **El worker es tonto.** Solo tiene el timer y abre un scope por tick. Toda la lógica vive en
   `IEmailSyncService`, de modo que los tests de integración la invocan directamente, sin esperas
   ni flakiness.
2. **El worker nunca usa `ICurrentUserProvider`.** El `UserId` sale de la tabla de conexiones,
   nunca del `HttpContext`.

Añadir un segundo worker (por ejemplo, uno dedicado al backfill) consiste en registrar otra
instancia del servicio; no requiere refactor.

### 2.5 Elección de librería

No se usa `Google.Apis.Client`. Solo hacen falta tres endpoints del proveedor
(`messages.list`, `messages.get`, `oauth2.token`) y un `HttpClient` con DTOs tipados y
`System.Text.Json`, con los registros añadidos a `BudgieJsonContext`. Evita una dependencia
enorme y los problemas de trimming/AOT que arrastra.

El permiso solicitado es de **solo lectura**: `gmail.readonly`. Budgie puede leer el buzón pero
nunca modificarlo ni marcar mensajes como leídos. Es un argumento de confianza relevante para un
producto que accede al correo del usuario.

## 3. Modelo de datos

Script nuevo: `src/apps/Budgie.Api/sql/003_email_import.sql`, siguiendo las convenciones de
`001_initial_schema.sql` y `002_import_runs.sql` (idempotente, identificadores entrecomillados,
registro en `_SchemaVersions`).

### 3.1 `EmailImportConnections`

```sql
CREATE TABLE IF NOT EXISTS "EmailImportConnections" (
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Provider" INT NOT NULL,
    "Email" VARCHAR(255) NOT NULL,
    "EncryptedCredentials" TEXT NOT NULL,
    "SyncCursor" VARCHAR(255),
    "SyncEnabled" BOOLEAN NOT NULL DEFAULT TRUE,
    "AutoCommitMinConfidence" DECIMAL(3,2) NOT NULL DEFAULT 0.85,
    "LastSyncAt" TIMESTAMP WITHOUT TIME ZONE,
    "LastError" VARCHAR(500),
    "LastSkippedCount" INT NOT NULL DEFAULT 0,
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE("UserId", "Provider", "Email")
);
CREATE INDEX IF NOT EXISTS "IX_EmailImportConnections_UserId_SyncEnabled"
    ON "EmailImportConnections"("UserId", "SyncEnabled");
```

`SyncCursor` es opaco por diseño (sección 2.3). `LastSkippedCount` expone los correos que no
casaron con ninguna regla sin ensuciar el esquema con filas sin importe.

### 3.2 `EmailImportRules`

```sql
CREATE TABLE IF NOT EXISTS "EmailImportRules" (
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Name" VARCHAR(100) NOT NULL,
    "AdapterKey" VARCHAR(50) NOT NULL,
    "Priority" INT NOT NULL DEFAULT 100,
    "IsEnabled" BOOLEAN NOT NULL DEFAULT TRUE,
    "AccountId" UUID NOT NULL REFERENCES "Accounts"("Id") ON DELETE CASCADE,
    "CategoryId" UUID REFERENCES "Categories"("Id") ON DELETE SET NULL,
    "TransactionType" INT NOT NULL,
    "Currency" VARCHAR(3) NOT NULL,
    "ExchangeRate" DECIMAL(18,4),
    "FromAddress" VARCHAR(255),
    "SubjectContains" VARCHAR(200),
    "BodyRegex" VARCHAR(500),
    "FieldPatterns" JSONB,
    "AutoCommit" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_EmailImportRules_UserId" ON "EmailImportRules"("UserId");
```

Un usuario puede tener varias reglas apuntando a cuentas distintas del mismo emisor; se
distinguyen por los filtros y por `Priority`.

`TransactionType` se limita a `Income` o `Expense` en v1, y el dominio rechaza `Transfer`, `Loan`
o `Repayment`. Motivo: esos tipos exigen un dato que un correo no puede aportar con fiabilidad — una
transferencia necesita una segunda cuenta, un préstamo una persona. Aceptarlos generaría
movimientos mal formados, así que el endpoint devuelve un `Error.Validation` explícito en lugar de
dejar que reviente en la base de datos.

### 3.3 `PendingEmailTransactions`

```sql
CREATE TABLE IF NOT EXISTS "PendingEmailTransactions" (
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "ConnectionId" UUID NOT NULL REFERENCES "EmailImportConnections"("Id") ON DELETE CASCADE,
    "RuleId" UUID REFERENCES "EmailImportRules"("Id") ON DELETE SET NULL,
    "MessageId" VARCHAR(255) NOT NULL,
    "ThreadId" VARCHAR(255),
    "Subject" VARCHAR(500),
    "Merchant" VARCHAR(255),
    "Notes" VARCHAR(500),
    "Amount" DECIMAL(18,2) NOT NULL,
    "Currency" VARCHAR(3) NOT NULL,
    "ExchangeRate" DECIMAL(18,4) NOT NULL DEFAULT 1,
    "Date" TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    "TransactionType" INT NOT NULL,
    "Confidence" DECIMAL(3,2) NOT NULL,
    "FieldConfidence" JSONB,
    "RawBody" TEXT,
    "Status" INT NOT NULL DEFAULT 0,
    "CreatedTransactionId" UUID REFERENCES "Transactions"("Id") ON DELETE SET NULL,
    "FailureReason" VARCHAR(500),
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    "ReviewedAt" TIMESTAMP WITHOUT TIME ZONE,
    UNIQUE("UserId", "MessageId")
);
CREATE INDEX IF NOT EXISTS "IX_PendingEmailTransactions_UserId_Status"
    ON "PendingEmailTransactions"("UserId", "Status");
```

`Status` usa el enum `PendingEmailStatus`, declarado junto a su único uso (en el entity
`PendingEmailTransaction`), siguiendo el precedente de `ExportFormat` y `BudgetThresholdStatus`:
`Pending = 0`, `Committed = 1`, `Dismissed = 2`, `Failed = 3`.

`EmailProvider`, en cambio, sí va a `Budgie.Shared/Enums/SharedEnums.cs`: es un enum persistido
y compartido entre Infrastructure, Presentation y la configuración, igual que `TransactionType` o
`AccountType`. Solo `Gmail = 0` en v1; añadir Outlook o IMAP es aditivo y no requiere migración.

`RawBody` se conserva porque permite reprocesar un mensaje con otra regla y entender por qué un
parseo falló. Es un compromiso consciente de privacidad: el cuerpo queda en la base de datos del
usuario, que ya contiene el resto de sus datos financieros.

### 3.4 Cambios en `Finances`

Dos cambios aditivos mínimos, necesarios para la idempotencia:

- `Transaction.Create(...)` acepta un parámetro opcional `string? externalUid`.
- `CreateTransactionCommand` acepta `string? ExternalUid = null`.

`Transactions` ya tiene la columna `ExternalUid` y el índice único
`IX_Transactions_UserId_ExternalUid` (creado en `002_import_runs.sql`).

### 3.5 Doble barrera de idempotencia

Es el punto más delicado del sistema. El orden por mensaje es siempre:

```
1. INSERT PendingEmailTransaction
     → viola UNIQUE("UserId","MessageId") → es duplicado → descartar
2. Dispatch CreateTransactionCommand con ExternalUid = "email:{provider}:{messageId}"
     → NpgsqlException SqlState 23505 → ya estaba commiteada → Status = Committed
3. UPDATE PendingEmailTransactions SET Status = 1, "CreatedTransactionId" = @id
```

El `provider` del `ExternalUid` es el **nombre en minúsculas** del enum (`email:gmail:18f2c...`), no
el entero, para que un `SELECT` manual sobre la base de datos sea legible.

Si el worker muere entre los pasos 2 y 3, el siguiente sync encuentra la fila en `Pending`,
reintenta el commit, y el índice único de `Transactions` le dice que ya estaba hecha. Una
transacción duplicada es imposible aunque el proceso muera en el peor momento.

**No se envuelve en una transacción SQL explícita**: la barrera de `ExternalUid` *es* el
mecanismo de crash-safety, y una transacción solo ocultaría los bugs.

## 4. Parseo y confianza

### 4.1 Del correo al texto

El parseo de MIME vive en Infrastructure, pero el adaptador lo consume desde Application. Para no
invertir la capa, el cruce es un puerto:

```csharp
public interface IMessageTextExtractor   // Application/Abstractions
{
    string ExtractPlainText(EmailMessage message);
}
```

`RawEmailReader` + `HtmlToTextConverter` lo implementan en Infrastructure. Los **normalizadores**
(`AmountNormalizer`, `DateNormalizer`) sí van en Application, porque es lógica pura que consume el
adaptador directamente y no depende de ningún formato de correo.

`RawEmailReader` acepta tanto un cuerpo suelto como el **fuente original** del mensaje tal y
como lo exporta el cliente de correo. Hace falta porque eso es exactamente lo que el usuario
pegará en el probador, y porque el cuerpo viene en base64 o quoted-printable con cabeceras MIME.
Decodifica la transferencia, separa las partes y prefiere `text/plain` sobre `text/html`.

`HtmlToTextConverter` convierte HTML a texto preservando estructura. Los correos bancarios son
tablas, y un `Regex.Replace("<[^>]+>", "")` a secas produce una línea ilegible, así que:

| Entrada | Salida |
|---|---|
| `</td>`, `</th>` | `\t` |
| `</tr>`, `</p>`, `</div>`, `</li>`, `<br>` | `\n` |
| `<script>`, `<style>` | eliminados con su contenido |
| Entidades (`&nbsp;`, `&amp;`, `&euro;`, `&#8364;`, `&#x20AC;`) | decodificadas |

Sin esto, las regex del usuario serían imposibles de escribir.

### 4.2 Localización

El parseo es *language-aware* porque el idioma del usuario determina el formato numérico y de
fecha, y los dos están intercambiados entre español e inglés. `AmountNormalizer` y
`DateNormalizer` (en `Application/Parsing/Normalization/`) reciben el idioma de
`ILanguageProvider`:

- es: `1.234,56` → `1234.56`
- en: `1,234.56` → `1234.56`

El caso `1.234` es el que rompe implementaciones: vale `1234` en español y `1.234` en inglés.
Sin esto, cada correo de banco con separador de miles acabaría en la base de datos.

### 4.3 Selección de regla

Reglas activas del usuario ordenadas por `Priority` ascendente; **gana la primera que casa** en
los tres filtros combinados con AND: `FromAddress` (coincidencia exacta o sufijo de dominio),
`SubjectContains` (case-insensitive) y `BodyRegex` (búsqueda sobre el texto plano).

Si ninguna casa: se registra en el log y se incrementa `LastSkippedCount`. **El mensaje no se
almacena**, porque hacerlo exigiría importe y fecha nulables para filas que no son transacciones.
La solución para recuperar los saltados es crear la regla y esperar a los correos siguientes.

### 4.4 `GenericRegexAdapter`

El único adaptador de v1. `FieldPatterns` es un JSONB de regex con grupos con nombre:

```json
{
  "amount":   "(?<amount>-?[0-9.,]+)\\s*(?<symbol>€|\$)?",
  "date":     "(?<date>\\d{2}/\\d{2}/\\d{4})",
  "merchant": "Comercio:\\s*(?<merchant>[^\\n]{2,40})",
  "direction": "abono|ingreso"
}
```

| Grupo | Significado |
|---|---|
| `amount` | Obligatorio. Se normaliza con `AmountNormalizer`. El signo negativo se ignora: el tipo sale de `direction` o de la regla |
| `date` | Opcional. Se normaliza con `DateNormalizer`; si falta, se usa la fecha del mensaje |
| `merchant` | Opcional. Va a `Notes` |
| `direction` | Opcional. Si casa → `Income`; si no → el `TransactionType` de la regla |
| `symbol` | Opcional. Símbolo de moneda detectado, para validar contra `Currency` de la regla |

La Regex se compila una vez y se cachea por `RuleId`; compilarla por mensaje es la diferencia
entre 1 ms y 40 ms con decenas de mensajes.

### 4.5 `ConfidenceScorer`

Confianza **por campo**, no un número opaco. Cada campo puntúa 0..1 con un motivo concreto, y
el total es la media ponderada:

| Campo | Peso | 1.0 cuando… | Penalizaciones |
|---|---|---|---|
| `amount` | 0.35 | el patrón captura un número que parsea a decimal válido | 0.5 si hay 2 coincidencias; 0.3 si hay 3 o más |
| `direction` | 0.20 | el patrón `direction` casó, o la regla fija el tipo de forma explícita | 0.0 si el patrón es ambiguo en ambos sentidos |
| `merchant` | 0.20 | texto no vacío y no es boilerplate del banco | 0.3 si cae en la lista negra (`Transferencia`, `Payment`, `Bizum`, `Pago`, `Operación`, `SEPA`, `Recibo`) |
| `date` | 0.15 | parseada y no futura | 0.5 si hay varias fechas distintas en el mensaje; 0.0 si no parsea |
| `currency` | 0.10 | el símbolo del correo coincide con `Currency`, o la regla la fija | 0.0 si el símbolo contradice a la regla |

```
Confidence = Σ (peso × score)
```

Auto-commit si `Confidence >= conexión.AutoCommitMinConfidence` (0.85 por defecto) **y** la regla
tiene `AutoCommit = true`.

`FieldConfidence` se persiste en JSONB para que la UI explique **por qué** algo quedó pendiente,
no solo cuánto.

Ejemplos:

- Regex que ancla el importe y todo lo demás cuadra → `0.35 + 0.20 + 0.18 + 0.15 + 0.10 = 0.98` → auto.
- Patrón de importe que casa tres veces (saldo anterior, importe, saldo nuevo) → `amount = 0.3` → total ≈ `0.72` → revisión.

Los pesos viven en una única clase de constantes `ConfidenceWeights`, para poder ajustarlos con
un test en lugar de buscarlos por el código.

## 5. Sincronización

### 5.1 Algoritmo

```
1. Cargar conexiones activas + reglas, agrupadas por usuario
2. Por cada conexión:
   a. Resolver IEmailProviderClient por connection.Provider
   b. ListNewAsync
        401 / invalid_grant  → SyncEnabled = false + LastError, continuar
        cursor inválido      → reset del cursor + ventana de 7 días, la dedupe limpia duplicados
        429 / 5xx            → backoff, NO guardar cursor, LastError, reintentar en el siguiente tick
   c. Guardar el cursor nuevo SOLO después de procesar todos los mensajes
   d. Por cada mensaje:
        INSERT staging → duplicado → descartar
        sin regla → LastSkippedCount++, siguiente
        el parseo lanzó → Status = Failed + FailureReason, siguiente
        confianza ≥ umbral && regla.AutoCommit
            → CreateTransactionCommand(ExternalUid)
            → 23505 → Status = Committed
            → Status = Committed + CreatedTransactionId
        si no → queda en Pending
   e. LastSyncAt = now, LastSkippedCount, LastError = null
```

**El cursor se guarda al final, nunca antes.** Si el proceso muere a mitad, el siguiente sync
relee los mismos mensajes y `UNIQUE` los descarta. Con el cursor guardado antes se perderían
correos; guardado después se repite trabajo inofensivo.

### 5.2 Por qué `CreateTransactionCommand` y no el repositorio

El auto-commit **despacha el comando de Mediator**, no escribe directamente. Así hereda sin
duplicar código:

- validación de `ControlStartDate` (`Transaction.BeforeControlStartDate`),
- matemática de cuotas e intereses,
- participantes,
- y el `TransactionCreatedEvent` que actualiza el saldo de la cuenta.

Una transacción importada por correo se comporta **exactamente igual** que una creada a mano. Si
el comando devuelve un error de validación, la fila pasa a `Failed` con ese mensaje y la UI lo
enseña para que el usuario lo corrija.

## 6. Endpoints y flujo OAuth

```
POST   /api/email-import/connections/authorize     RequireAuthorization → { authorizationUri }
GET    /api/email-import/connections/{provider}/callback?code&state    ← SIN RequireAuthorization
DELETE /api/email-import/connections/{provider}    RequireAuthorization → 204
GET    /api/email-import/connections               RequireAuthorization → [estado]
GET    /api/email-import/rules                     RequireAuthorization
POST   /api/email-import/rules                     RequireAuthorization
PUT    /api/email-import/rules/{id}                RequireAuthorization
DELETE /api/email-import/rules/{id}                RequireAuthorization
POST   /api/email-import/rules/preview             RequireAuthorization
GET    /api/email-import/inbox                     RequireAuthorization → pendientes paginados
POST   /api/email-import/inbox/{id}/approve        RequireAuthorization
POST   /api/email-import/inbox/{id}/dismiss        RequireAuthorization
POST   /api/email-import/inbox/approve-batch       RequireAuthorization
POST   /api/email-import/sync                      RequireAuthorization → sincroniza ya
```

**El callback va sin `RequireAuthorization`, y es intencionado.** La redirección del proveedor
llega desde el navegador del usuario, que en ese instante no tiene el JWT de Budgie (vive en
localStorage, no en una cookie que viaje al servidor). Por eso la autenticación del callback no
puede apoyarse en `[Authorize]`: se apoya **solo** en el `state`.

`state` es un token opaco firmado con la clave del servidor que lleva dentro `userId`, un nonce
aleatorio y un timestamp. El callback valida firma y antigüedad, consume el nonce (un solo uso) y
resuelve el `userId` desde ahí. Sin ese cuidado, alguien con un `code` ajeno podría engancharse
la conexión a su propia cuenta. Se documenta aquí porque es el único endpoint del módulo sin
`[Authorize]` y va a destacar en la revisión de código.

El redirect URI se deriva del host de la petición, así que el usuario registra
`https://<su-dominio>/api/email-import/connections/gmail/callback` en su consola de OAuth.

## 7. Probador de reglas

Sin esto, escribir una `generic.regex` sería a ciegas.

```
POST /api/email-import/rules/preview
{ "ruleId": "..." }            +  cuerpo o fuente original pegado
→ { plainText, fields, fieldConfidence, confidence }
```

No persiste nada. Muestra el texto plano que produce el conversor, los campos extraídos y la
confianza por campo, para iterar antes de conectar el buzón.

Es también la vía para descubrir que un correo viene en un HTML que el conversor no parsea bien.

## 8. Interfaz

Módulo nuevo `src/apps/Budgie.Web/src/modules/email-import/`. **Una sola página con dos
pestañas** (Tabs de shadcn): *Pendientes (N)* y *Reglas*. La conexión vive en Ajustes, porque es
configuración de nivel usuario y no flujo de trabajo.

```
pages/EmailImportInbox.tsx             → /email-import
components/EmailRuleForm.tsx
components/EmailRuleTester.tsx
components/PendingTransactionRow.tsx
components/ConfidenceBadge.tsx
components/FieldConfidenceBadges.tsx
hooks/useEmailConnections.ts · useEmailRules.ts · usePendingEmails.ts
```

Ajustes: `EmailImportSettingsCard.tsx` con conectar/desconectar, umbral de auto-commit, estado
de la conexión (última sincronización, error, `LastSkippedCount`) y "Sincronizar ahora".

También hay que registrar `/email-import` en `App.tsx` y en `routes.ts` (lazy **y**
`lazyModules`, que es lo que evita el flash de navegación), más la entrada en `sidebar-config.ts`
con el contador de pendientes.

### 8.1 La fila de revisión

Es la pieza que más importa, porque es donde el usuario descubre **por qué** algo quedó pendiente:

- **Badges por campo** coloreados según `FieldConfidence` — Importe ✓, Fecha ✓, Comercio ⚠, signo ?.
  Con **texto además de color**, nunca solo color (requisito de accesibilidad).
- Campos editables en línea: cuenta, categoría, importe, fecha, comercio, notas, tipo.
- "Ver correo original" despliega `RawBody` como texto plano preformateado.
- **Aprobar** / **Descartar** por fila, y **"Aprobar los de alta confianza"** en lote, con
  diálogo de confirmación y solo los que superan el umbral.
- Al descartar, "¿Guardar como regla?" prellena `EmailRuleForm` con remitente y asunto ya
  capturados. Cierra el ciclo aprender-del-uso: añadir un banco nuevo no exige escribir una
  regex desde cero.

## 9. Errores y resiliencia

| Caso | Comportamiento |
|---|---|
| Credencial revocada (401) | `SyncEnabled = false` + `LastError`; Ajustes muestra "Reconectar"; los demás usuarios no se ven afectados |
| Cursor inválido (historial expirado) | reset + ventana de 7 días; `UNIQUE` evita duplicados |
| 429 / 5xx del proveedor | backoff exponencial, cursor **no** guardado, reintento en el siguiente tick |
| Un mensaje no parsea | `Failed` + `FailureReason`; el sync continúa con los demás |
| `CreateTransactionCommand` con error de validación | `Failed` con ese mensaje, visible y corregible |
| Caída a mitad del sync | cursor no guardado → se relee → dedupe; transacción huérfana detectada por 23505 |
| `EncryptionKey` ausente | se deriva de `Jwt:Secret` por SHA-256, con warning en el log |
| Regla apuntando a cuenta borrada | `ON DELETE CASCADE` elimina la regla |
| Un único usuario con bucle de parseo roto | el error queda en su fila, no en el sync de los demás |

Logging con `[LoggerMessage]` en `Infrastructure/Logging/LogMessages.cs`: `SyncStarted`,
`SyncCompleted`, `MessageSkippedNoRule`, `MessageParseFailed`, `MessageAutoCommitted`,
`MessageStaged`, `CommitFailed`, `ProviderAuthFailed`, `KeyDerivedFromJwtSecret`.

## 10. Configuración

```json
"EmailImport": {
  "SyncIntervalMinutes": 10,
  "EncryptionKey": "<base64 32 bytes>",
  "Providers": {
    "gmail": { "ClientId": "...", "ClientSecret": "..." }
  }
}
```

`Providers` es un diccionario indexado por el `EmailProvider` resuelto como clave de
configuración, de modo que añadir un proveedor es añadir una entrada, no tocar código.

El redirect URI se deriva del host de la petición, así que el usuario registra
`https://<su-dominio>/api/email-import/connections/{provider}/callback` en su consola de OAuth.
El `state` va firmado y liga el callback con el usuario.

## 11. Seguridad

- **Cifrado en reposo**: `AesSecretProtector` con AES-256-GCM. La credencial se almacena como
  `v1:<base64(nonce)>:<base64(ciphertext)>:<base64(tag)>`.
- **Scope de solo lectura** (`gmail.readonly`). Budgie nunca escribe en el buzón.
- **`state` firmado** en el flujo OAuth, para impedir CSRF en el callback.
- Las credenciales nunca se devuelven en ninguna respuesta de la API; el endpoint de estado solo
  expone el email y metadatos.

## 12. Tests

### Unitarios

- `HtmlToTextConverterTests`: tablas → tabs y saltos correctos, entidades, `<br>`, `<script>` eliminado.
- `RawEmailReaderTests`: base64, quoted-printable, multipart con alternativa `text/plain`.
- `AmountNormalizerTests`: `1.234,56` es, `1,234.56` en, `45,20 €`, `-12.00`, y el caso
  `1.234` con el mismo resultado según idioma.
- `DateNormalizerTests`: `02/10/2026`, `2026-10-02`, `2 oct 2026`, no parseables, fechas futuras.
- `GenericRegexAdapterTests`: captura de cada campo, dirección por `direction`, merchant en lista
  negra → score bajo, patrón que no casa → score 0, caché de regex por `RuleId`.
- `ConfidenceScorerTests`: tabla de casos sobre los pesos y el umbral.
- `AesSecretProtectorTests`: round-trip, nonce distinto en cada cifrado, ciphertext corrupto lanza,
  clave incorrecta lanza.
- `EmailImportRuleTests`: validación de dominio (priority ≥ 0, `FieldPatterns` obligatorio para
  `generic.regex`, regex inválida rechazada).
- `EmailSyncServiceTests` con un `IEmailProviderClient` fake: dedupe por `MessageId`, el cursor no
  se guarda si algo falla, se respeta el umbral, 23505 se trata como ya commiteada, 401 deshabilita
  la conexión.

### Integración

Siguiendo el patrón de `002_import_runs` (`WebApplicationFactory` + contenedor PostgreSQL):

- el callback OAuth persiste la credencial cifrada y se verifica que **en claro no aparece en la
  base de datos**;
- sync con tres mensajes seed → 1 auto-commit, 1 pending, `LastSkippedCount == 1`;
- **re-ejecutar el sync no crea nada nuevo**: el test más importante del sistema;
- aprobar un pendiente crea exactamente una transacción y marca la fila;
- desconectar saca la conexión de `GetEnabledConnections`.

### E2E

Playwright + MSW, siguiendo `e2e-testing`:

- conectar el proveedor, crear una regla `generic.regex`, sincronizar, ver el badge de pendientes;
- revisar un pendiente de baja confianza y aprobarlo hasta verlo en `/transactions`.

## 13. Fuera de alcance (v1)

- Adaptadores `bank.*`: el puerto `IBankAdapter` existe y queda registrado, pero v1 solo
  implementa `generic.regex`. Son aditivos.
- Otros proveedores de correo: el puerto `IEmailProviderClient` está listo, no la implementación.
- Detección de cuotas: siempre `Installments = 1`. La UI de revisión muestra `RawBody`, así que si
  el correo dice "3 cuotas" el usuario lo ve y lo fija.
- Conversión de divisas: el tipo de cambio lo declara la regla; no hay consulta de tipos de cambio.
- Buzón dedicado + webhook entrante.
- Reprocesar correos históricos sin regla.
- Notificaciones de "se importaron N transacciones".

## 14. Decisiones de alcance tomadas durante el brainstorming

| Pregunta | Respuesta |
|---|---|
| Mecanismo de recepción | Polling con la API del proveedor desde un `BackgroundService` |
| Dónde corre | Dentro de `Budgie.Api`, sin cambios de despliegue |
| Origen de los correos | Varios emisores con formatos distintos → motor de reglas |
| Confirmación | Auto-commit con confianza alta, revisión en el resto |
| Resolución de cuenta | Cuenta fija declarada por la regla |
| Alcance del buzón | Por usuario, OAuth individual |
| Shape del motor de reglas | `generic.regex` configurable; los adaptadores por banco quedan para v2 |
| Ejemplos de correo necesarios | No. El probador de reglas permite hacerlo todo desde la UI |
