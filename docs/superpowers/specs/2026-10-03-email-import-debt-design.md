# Importación por correo — Deuda técnica y mejoras futuras

**Fecha:** 2026-10-03
**Estado:** Propuesta — no aprobado
**Origen:** revisión de código del módulo `Budgie.Modules.EmailImport` (`feat(finances)`, commit `d954267`)
**Plan de referencia:** `docs/plans/03-email-import.md`
**Spec de referencia:** `docs/superpowers/specs/2026-10-02-email-import-design.md`

> Esta spec **no propone una feature**. Recoge lo que quedó sin hacer, lo que se hizo a medias y
> lo que la revisión encontró. No es un plan de trabajo: es el registro honesto de las
> limitaciones conocidas del módulo, agrupadas por urgencia, para que nadie las descubra en
> producción.
>
> Cada punto dice **qué pasa hoy**, **por qué importa** y **qué habría que hacer**. Ninguno
> bloquea el uso normal del módulo, y todos están medidos o verificados, no supuesta.

---

## 1. Bloqueantes para despliegue real

Ninguno estaba en esta lista al empezar la revisión; los cuatro aparecieron durante ella y ya
se corrigieron. Se dejan aquí porque **revelan una clase de fallo que conviene no repetir**.

### 1.1 El módulo nunca se probó contra el mundo real

**Qué pasaba.** Toda la verificación pasó por un `IEmailProviderClient` sustituido por un stub. En
el punto exacto donde el contrato es con Gmail, nadie miró. Consecuencia: las rutas REST eran
`users/messages/list`, `users/history/list`, `users/getProfile` — **identificadores de método RPC,
no URLs**. La REST real es `gmail/v1/users/{userId}/<resource>`, así que **toda lectura real habría
devuelto 404** y el módulo habría estado funcionalmente muerto.

**Por qué importa.** Un fake puede responder correctamente a una URL incorrecta. El test que faltaba
no es "más unit tests" sino **una capa HTTP**: un `HttpMessageHandler` de recording que afirme las
URLs, y una prueba de humo contra un buzón real antes de documentar el módulo como soportado.

**Estado.** Corregido con un helper `MePath()` y 4 tests de regresión que afirman las cuatro rutas.
**Pendiente**: la prueba de humo manual contra un buzón real sigue sin hacerse (ver §5.1).

### 1.2 Una conexión nueva nunca anclaba el cursor

**Qué pasaba.** El anclaje al `historyId` solo existía en el `catch` de `CursorExpired`, que en
Gmail únicamente se dispara cuando el cursor **no es nulo**. Una conexión recién conectada releía la
ventana de 7 días **en cada tick, para siempre**, descartando cada relectura por el `UNIQUE` del
staging. Coste, cuota de API y `LastSkippedCount` inflado, creciendo sin límite.

**Por qué importa.** Degradación silenciosa: no falla, no lanza, no aparece en los tests porque el
fake se configuraba con el cursor ya puesto. Un módulo que "funciona pero va cada vez peor" es el
peor modo de fallo posible, y solo aparece en producción.

**Estado.** Corregido: la rama de cursor nulo ancla antes de listar la ventana, con 4 tests, uno de
ellos afirmando el orden `profile → list`.

### 1.3 `redirect_uri` se construía con el esquema equivocado detrás del proxy

**Qué pasaba.** El despliegue documentado en `AGENTS.md` termina TLS en un proxy inverso, así que la
petición llega a la app con `Scheme=http` aunque el navegador haya usado `https`. El `redirect_uri`
se derivaba de `request.Scheme`, dando `http://…` donde Google espera `https://…` →
`redirect_uri_mismatch` y **conectar el buzón imposible**.

**Por qué importa.** Era el único hallazgo que habría hecho inútil la feature entera en el
despliegue documentado, y no se detecta en local.

**Estado.** Corregido con `UseForwardedHeaders`, registrado antes de `UseExceptionHandler`.
Verificado levantando la API contra PostgreSQL real y midiendo las dos situaciones:

```
acceso directo:          http://127.0.0.1:5057/.../callback
detrás de proxy TLS:     https://budgie.midominio.com/.../callback   ✓
```

### 1.4 Arreglar una regex rota no surtía efecto hasta reiniciar

**Qué pasaba.** La caché de regex compiladas se claveaba por `rule.Id`, y `UpdateRule` persiste el
**mismo Id** con patrones nuevos. Un usuario que corregía una regex rota seguía viendo la patrón
antigua en el sync y en el probador, indefinidamente. Peor: el test existente *"same rule compiles
once"* **fijaba el comportamiento defectuoso**, porque nunca cambiaba los patrones entre llamadas.

**Por qué importa.** Es el fallo más caro de diagnosticar para el usuario: "arreglé el patrón y sigue
igual", sin ninguna señal de por qué. Un test que no falla al reintroducir el defecto está fijando
el defecto, no previéndolo.

**Estado.** Corregido con clave `(RuleId, hash(patterns))` y tope de 256 entradas. Añadidos 3 tests
que sí cambian los patrones.

---

## 2. Funcionalidad incompleta o ausente

Estas son carencias reales de la feature, no deuda técnica. La spec original las daba por hechas y
no se implementaron.

### 2.1 No hay forma de cambiar el umbral de auto-commit

**Qué pasa.** `AutoCommitMinConfidence` existe en la tabla, es un parámetro de `EmailConnection.Create`,
y **no hay endpoint que lo actualice**. Solo hay GET y DELETE de conexiones. El slider del frontend
está en **solo lectura** con descripción explícita.

**Impacto.** Hoy el umbral solo se cambia por base de datos. Para el usuario es un parámetro que
parece configurable y no lo es — peor que no ofrecerlo.

**Qué hacer.** `PUT /api/email-import/connections/{provider}/threshold` (o extender el endpoint de
estado con un PATCH). El dominio ya tiene `ChangeAutoCommitThreshold(decimal)` validado a `[0, 1]`.
El slider del frontend pasa a editable. Test: el valor persiste y el umbral afecta de verdad a la
decisión de auto-commit en el siguiente sync.

### 2.2 La UI no puede mostrar qué buzón está conectado

**Qué pasa.** `EmailCredentials` no incluye la dirección de correo, así que la conexión se guarda con
`Email = "connected"`. Ese valor está además en el `UNIQUE("UserId","Provider","Email")`.

**Impacto.** El usuario no ve qué buzón conectó. Con un buzón es cosmético; con varios, no puede
distinguirlos. Y un placeholder que no parece un email es preferible a uno con formato válido
inventado — esa decisión fue correcta y se mantiene.

**Qué hacer.** `users.getProfile` ya se llama para el `historyId` y devuelve `emailAddress` en la
**misma respuesta**. No hace falta ninguna llamada nueva ni permisos extra (`gmail.readonly` ya lo
cubre). Es un campo más en `EmailCredentials` y su uso en `CompleteConnection`. Coste: bajo.

### 2.3 Un símbolo de moneda contradictorio se auto-aprueba

**Qué pasa.** Con `Currency = 0.0` y los pesos de la spec, un correo en `$` con una regla EUR da
confianza **0.90**, por encima del umbral 0.85. La transacción se crea etiquetada EUR sin que nadie
mire nada. El importe es correcto; **la etiqueta de moneda no**.

**Por qué importa.** Es el único camino del sistema que puede dejar dinero bajo la etiqueta
equivocada sin intervención humana. Los otros dos lo previenen: el umbral y el filtro de regla.

**Por qué está así.** Los pesos los fijó la spec §4.5 y no son un error de implementación — son una
decisión de calibración. Tres de las siete penalizaciones no llegan a cruzar el umbral por sí solas:

| penalización | pérdida | confianza resultante | destino |
|---|---|---|---|
| `amount` 0.5 / 0.3 / 0.0 | 0.175 / 0.245 / 0.35 | 0.825 / 0.755 / 0.65 | **revisión** ✓ |
| `direction` 0.0 | 0.20 | 0.80 | **revisión** ✓ |
| `merchant` 0.3 (lista negra) | 0.14 | 0.86 | auto |
| `date` 0.5 (sin patrón) | 0.075 | 0.925 | auto |
| `currency` 0.0 (símbolo contradice) | 0.10 | 0.90 | auto |

**Qué hacer.** Es una decisión de producto, no de ingeniería, y hay varias salidas honestas:
(a) subir el peso de `currency` para que la contradicción fuerce revisión; (b) añadir una
penalización compuesta (símbolo + importe) que cruce el umbral; (c) tratar la contradicción como
`ParseOutcome.NoMatch` en lugar de como penalización, porque es un dato **cualitativamente**
distinto de "el regex no ancla bien"; (d) mandarlo a revisión con una advertencia visible.
La opción (c) es la que más recomiendo: no es "menos seguro", es "otro tipo de hecho". Requiere
decidirlo con el usuario antes de tocar `ConfidenceWeights`.

### 2.4 La contradicción de moneda no se explica en la UI

Aunque se decida mantener el auto-commit, la fila de revisión y el probador deberían **decir** que el
símbolo del correo no coincide con la moneda de la cuenta. Hoy `Explanation` lo reporta en texto
pero no como advertencia destacada, y solo al abrir el probador.

### 2.5 Un `AdapterKey` mal escrito falla en silencio

**Qué pasa.** El dominio acepta cualquier string ≤ 50 como `AdapterKey` (no valida contra el
registro). El probador **sí** lo detecta (`EmailImport.UnknownAdapter`), pero el sync falla cada tick
vía `registry.Get → InvalidOperationException → failed++`, contando en `Failed` sin explicar por qué.

**Qué hacer.** Validar `AdapterKey` contra el registro en `CreateRule`/`UpdateRule`, que es donde se
puede dar un error accionable. El dominio no conoce el registro, así que la validación va en el
handler de Application, que sí lo tiene.

### 2.6 `ApproveBatch` no acota `Take`

`ApproveBatchCommand.Take` acepta cualquier entero y el handler no lo limita. Un `take: 100000` hace
un bucle de N aprobaciones, cada una con su dispatch. Acotarlo al rango de `GetHighConfidenceAsync`
(que ya tiene tope en SQL).

---

## 3. Violaciones de convención (AGENTS.md)

### 3.1 `NpgsqlException` se captura en Application

**Dónde.** `Application/Inbox/EmailSyncService.cs` y
`Application/Inbox/ApprovePending/ApprovePendingCommandHandler.cs`.

AGENTS.md prohíbe explícitamente `Npgsql` en Application. Es una violación real, no matizable.

**Por qué se hizo.** La barrera de idempotencia necesita distinguir `23505` (ya importada) de otros
errores. Sin distinguirlo, una transacción ya creada se reportaría como fallo y se reintentaría en
bucle.

**Por qué está justificado, pero no es la solución final.** El repo tiene el precedente correcto:
`Finances/Application/Import/InvalidImportFileException.cs` existe precisamente para no filtrar el
tipo de infraestructura. La alternativa que falta es una excepción neutra en `Budgie.Shared`
(`UniqueConstraintViolationException`), traducida en `TransactionRepository.AddAsync`. Diez líneas,
un solo punto de traducción, y Application deja de referenciar Npgsql.

**Qué hacer.** Traducir en el repositorio de Finanzas, capturar la excepción neutra en Application.
**Refactor que toca Finanzas**; por eso no se hizo en esta feature. Requiere revisar que ningún
`catch` existente dependa del tipo concreto.

### 3.2 Application depende de `IOptions<EmailImportOptions>`

`SyncNowCommandHandler` lee `IOptions<EmailImportOptions>`, que vive en Infrastructure, para saber si
el worker está activo. El comentario que lo justifica equipara `LogMessages` con `Options`, pero la
equiparación es falsa: `LogMessages` es un patrón **sancionado expresamente por AGENTS.md**,
mientras que ningún handler de Application de Finanzas referencia hoy un tipo `Options` de
Infrastructure.

**Qué hacer.** Mover `EmailImportOptions` a Application (es un POCO de configuración, no de
infraestructura), o esconder el flag detrás de un port propio. Lo primero es más simple.

### 3.3 `PendingEmailStatus` sigue en Domain pero se usa en Presentation

`InboxEndpoints` hace binding del enum de Domain. El propio plan decía: "si el enum acaba usándose en
Presentation, moverlo a `SharedEnums.cs`". Se usa, y no se movió. Es un enum persistido y de la API,
igual que `TransactionType`.

### 3.4 `EmailImportRule.Currency` es string crudo dentro del módulo

El dominio valida con `CurrencyCode.From` pero **guarda el string**. `PendingEmailTransaction` sí usa
`Money`/`CurrencyCode`. Es inconsistencia dentro del mismo módulo, contra el criterio de no
esparcir primitivas. El DDL (`VARCHAR(3)`) aguanta ambas formas.

---

## 4. Sobre-ingeniería (candidatas a simplificación, no a corrección)

Ninguna es un bug. Se listan para que la decisión sea informada.

### 4.1 `ScopedCurrentUserProvider` reemplaza al del host para todo el árbol

Un módulo de feature opcional sustituye una preocupación transversal (quién es el usuario actual)
para **todos** los servicios, sujeto al orden de registro en `Program.cs`. Si alguien añade otro
`AddScoped<ICurrentUserProvider, …>` después, el `Push` queda invisible para los handlers sin error
de compilación.

Funciona (el fallback al claim es idéntico al del `CurrentUserProvider` original, verificado claim a
claim), pero **el sitio es el equivocado**: la preocupación es del host o de `Budgie.Shared`, no del
módulo. El módulo debería registrar solo el `Push`.

### 4.2 La doble capa de recuperación de cursor

Había recuperación interna en `GmailEmailProvider` **y** recuperación en `EmailSyncService`. La del
servicio sobrevivía solo porque los tests la ejercitaban contra el fake: contra Gmail real es
inalcanzable, porque el provider se traga su propio `CursorExpired`. Duplicación que daba falsa
confianza ("resuelto en dos capas"). Corregido al unificar, pero el patrón de "una capa que solo
existe para los tests" conviene reconocerlo.

### 4.3 `EmailImportOptions.Providers` es un diccionario jamás leído

Se declara, se documenta y se configura (`docker-compose.yml`, `.env.example`), y `GmailOptions` se
bindea directamente. La indirección no aporta nada hoy. Se conserva porque es el sitio natural donde
encajaría un segundo proveedor, pero conviene documentar que está sin usar.

### 4.4 El puerto `IEmailProviderClient` obliga a fabricar un `EmailConnection` de mentira

`BuildAuthorizationUriAsync` recibe un `EmailConnection` que el provider **no usa** — el
`GetAuthorizationUriCommandHandler` construye uno provisional con `Email = "pending"`. La firma del
puerto se derivó de un ejemplo, no de una necesidad. Un puerto que exige un objeto de mentira para una
operación que no lo necesita suele indicar que la firma está mal.

### 4.5 `IBankAdapter` con una sola implementación

Justificable porque la spec lo exige y el coste real son 3 ficheros. Se mantiene como costura de
extensión: los adaptadores por banco son aditivos.

---

## 5. Verificación pendiente

### 5.1 Prueba de humo contra un buzón real — **la más importante de esta lista**

Todo el módulo se ha verificado con stubs y HTTP simulado. El plan incluía "conectar un buzón de
prueba y recorrer el ciclo completo con la UI" como paso manual, y **no se ha hecho**.

Es exactamente el hueco que dejó pasar §1.1. Los fakes no pueden detectar una URL mal, un formato de
respuesta que cambió, un parámetro que Google renombró, ni un scope insuficiente.

Checklist mínimo cuando se haga:
- registrar el redirect URI correcto en Google Cloud Console;
- `POST /connections/authorize` → URL con `access_type=offline` y `state`;
- completar el consentimiento → callback → `/settings?emailImport=connected`;
- confirmar que **el mensaje no queda marcado como leído** (la garantía del scope `gmail.readonly`);
- una regla real: probar con un correo real, guardar, sincronizar, revisar;
- reiniciar el contenedor a mitad de un sync y verificar que no duplica;
- revocar el acceso desde la consola de Google y comprobar que Ajustes pide reconectar y que **otro**
  usuario sigue sincronizando.

### 5.2 Cobertura de huecos tests

| # | Hueco | Estado |
|---|---|---|
| D1 | Capa HTTP de `GmailApiClient` | Cubierto tras §1.1 (4 tests de rutas) |
| D2 | Conexión nueva ancla el cursor | Cubierto tras §1.2 |
| D3 | Regla editada recompila | Cubierto tras §1.4 |
| D4 | Cancelación a mitad del dispatch | **No cubierto.** El fix de `OperationCanceledException` no tiene test. Debería: una parada de contenedor en mitad de lote no debe manchar filas `Failed` |
| D5 | Umbral de moneda | **No cubierto.** Ver §2.3: si se decide mantener 0.90 → auto, debería haber un test que lo **fije como comportamiento consciente** |

### 5.3 Bugs encontrados por los tests, sin causa raíz investigada

`RawEmailReader` devolvía las cabeceras MIME como cuerpo cuando una parte `text/plain` venía vacía
(corregido, con test). Lo interesante: **nadie lo había visto**, y los emisores reales sí mandan esa
parte vacía. Es una muestra de que los parsers de MIME tienen más casos de los que cubren los tests.

---

## 6. Deuda menor

- **El umbral `0.85` vive en tres sitios**: `EmailConnection.DefaultAutoCommitMinConfidence`,
  `ConfidenceWeights.DefaultAutoCommitThreshold` y el `DEFAULT 0.85` del DDL. La spec pedía "un solo
  sitio para poder ajustarlo con un test".
- **El parser de cabecera `From` está triplicado**: `EmailMessage.GetFromAddress`,
  `EmailImportRule.ExtractAddress` y `GmailMessageMapper.NormalizeFrom`. Hoy es benigno (Gmail
  normaliza en el borde y la regla re-normaliza), pero es la clase de bug que aparece cuando una de
  las tres se afina.
- **`CurrentUserProvider` sigue existiendo en `src/apps/Budgie.Api/Infrastructure/`** sin registrarse
  en ningún sitio. Su `GetUserId()` lanza `UnauthorizedAccessException`, el reemplazo lanza
  `InvalidOperationException`, y nadie captura ninguno. Borrarla o dejar un comentario que explique por
  qué se conserva.
- **`RawEmailReader` prefiere el primer `text/plain` aunque sea un adjunto** (`Content-Disposition` no
  se inspecciona).
- **`&#X20AC;` (hex en mayúscula) no se decodifica**: el regex de entidades solo acepta `#x` en
  minúscula. HTML válido usa cualquiera de las dos formas.
- **`PendingEmailTransaction` recorta a `""` un `merchant` en blanco** en vez de normalizarlo a `null`
  (el `TrimOrNull` a `null` es de `EmailImportRule`). Inconsistencia interna.
- **`GetFailedAsync` materializa todas las filas fallidas en memoria** (con `RawBody`) para paginar
  después. Documentado como deliberado; sigue siendo un riesgo de payload.
- **`EmailParser` empata la prioridad por orden de lista** sin desempate determinista. Dos reglas con
  la misma prioridad dependen del orden de la lista de la DB.
- **`jsonb` normaliza al almacenamiento** (reordena claves, re-escapa). Medido: enviado
  `{"zz":"1","amount":"([0-9]+)","aa":"2"}` → leído `{"aa": "2", "zz": "1", "amount": "([0-9]+)"}`.
  Ningún consumidor puede comparar strings JSON crudos. Relevante para el frontend al editar reglas.

---

## 7. Deuda del frontend

### A11y

- **Los `Select` de Base UI del formulario de reglas no están asociados a su `Label`**
  (`htmlFor`/`id`). **Ya corregido** en `EmailRuleForm` y `EmailRuleTester` replicando el patrón que ya
  existía en `PendingTransactionRow`, lo que requirió añadir `id` a `CategoryCombobox` (componente
  compartido).
- **Errores inline sin `aria-describedby`**: el `<p>` de error del input de importe no está referenciado,
  así que un lector de pantalla no lo lee al enfocar.
- **Resultados asíncronos sin `aria-live`**: la explicación "por qué quedó pendiente" y el resultado
  del probador aparecen sin `role="status"`.
- **Skeletons sin `role="status"`**: la carga de la bandeja y de la tarjeta de ajustes no se anuncian.

### Corrección

- **Las regex no se validan en cliente**: los 6 campos regex viajan crudos al servidor; un error de
  sintaxis solo se revela tras `POST /rules` con un 400 cuyo mensaje es de .NET en inglés. Escribir
  regex "a ciegas" es justo lo que el probador de la spec §7 quería evitar. **Cuidado**: la sintaxis
  JS no es la de .NET, así que es *early feedback*, no sustituto del 400 del servidor.
- **Parseo de comas solo reemplaza la primera** en el input de importe: `"1,234,56"` → `NaN`. Trampa
  para usuarios en formato es-ES con miles.
- **Dos convenciones de fecha conviven**: `formatLocal(parseDbDate(...))` en la fila frente a
  `format(new Date(...))` en la tarjeta de ajustes. Con un timestamp `Z` divergen. El E2E usa fixtures
  sin `Z`, así que nada lo detecta.
- **Ventana de doble clic al aprobar**: `isPending` pasa a `false` en el éxito, antes de que termine el
  refetch de la invalidación; la fila sigue visible y un segundo clic produce un 404.

### Rendimiento

- **`EmailRuleForm` tiene 552 líneas** con tres componentes, 13 campos de estado, validación,
  construcción del body y publicación del borrador.
- **`parsePatterns` re-parsea en cada render** y descarta el resultado salvo en el mount.
- **Re-render de toda la pestaña "Reglas" en cada keystroke**: `onDraftChange` → `setRuleDraft` en el
  padre → re-render de página, probador y lista.

### Varios

- **Spinners inalcanzables en `AlertDialogAction`**: la primitiva cierra el diálogo al hacer clic, así
  que `disabled={isPending}` y el `Loader2` nunca se ven. Código muerto con falsa sensación de feedback.
- **El probador conserva el resultado anterior** al cambiar de regla, y el trigger puede mostrar "Form
  draft" con `draft == null` (el botón se inhibe, pero el label engaña).
- **El estado (pestaña, página, filtro) no vive en la URL**: al compartir o refrescar se pierde. El
  repo hace lo mismo en otras páginas, así que es consistencia.
- **`window.location.replace("/login")`** en vez de `useNavigate`: recarga completa. Aceptable para
  "sesión perdida", pero inconsistente con el router.
- **Botones de paginación `size="sm"`**: ~28-32 px, por debajo del touch target de 44 px, en la página
  principal de revisión.

---

## 8. Lo que está bien y no hay que tocar

Para que la lista no se lea como "todo está mal":

- **La doble barrera de idempotencia** funciona y está probada con `Received.InOrder`, no contando
  llamadas. Un test corre las dos implementaciones reales y compara los `ExternalUid` byte a byte.
- **`OAuthStateService`**: firma HMAC con comparación constante, nonce quemado antes de devolver el
  payload, provider sellado dentro, caducidad de 10 minutos. Es exactamente lo que exige que el
  callback vaya sin `[Authorize]`.
- **`AesSecretProtector`**: AES-256-GCM con envelope versionado, `Unprotect` que **lanza** ante tag
  inválido en vez de devolver basura, warning único de fallback con `Interlocked`.
- **La dirección de capas del parseo**: MIME/HTML en Infrastructure tras el puerto
  `IMessageTextExtractor`, normalizadores puros en Application. Era el error de capas más probable del
  plan y quedó resuelto.
- **La neutralidad de proveedor es estructural y auditable con un grep**: cero identificadores de
  proveedor fuera de Infrastructure.
- **`ScopedCurrentUserProvider`** funciona: el fallback al claim es idéntico al del provider original,
  verificado claim a claim.
- **No hay `dangerouslySetInnerHTML` en todo el repo.** El HTML del correo se procesa en backend y al
  frontend llega texto plano. El riesgo de XSS está cerrado **por diseño**.
- **La paridad es↔en de las claves i18n**, verificada por script contra el árbol real.
- **El repositorio, el worker y los handlers respetan tenancy, `LoggerMessage` source-gen y AOT** sin
  excepción en la tabla de capas de AGENTS.md.

---

## 9. Lecciones del proceso

No son acciones, pero condicionan cómo hacer el siguiente módulo.

1. **Un fake puede responder correctamente a una URL incorrecta.** "Verificado con stubs" y
   "verificado contra el mundo real" son cosas distintas, y la diferencia salió cara (§1.1).
2. **Una aserción floja oculta defectos.** `getByText(/automatic/i)` en el E2E de `SyncAlreadyScheduled`
   se satisfacía con el badge "Automatic sync" de la tarjeta, que aparece antes en el DOM. El test
   pasaba sin comprobar el toast. Al hacerlo estricto, apareció debajo un mock que convertía un 400
   en un 200 con el error anidado. **Toda aserción debe fallar cuando lo que dice su nombre no ocurre.**
3. **Un test que no falla al reintroducir el defecto está fijando el defecto.** El test de caché de
   regex comprobaba "misma regla → una compilación" y nunca cambiaba los patrones, así que fijaba
   exactamente el bug de §1.4.
4. **Comparar dos catálogos no valida el tercero.** La verificación "es y en tienen las mismas claves"
   era cierta y no detectó una clave que **no existía en ninguno** de los dos
   (`emailImport.rules.patternRequired`, que se veía en pantalla literal). Hay que comparar contra las
   claves *usadas en código*.
5. **El código no testeable esconde bugs.** `GmailApiClient` era `sealed` y por eso nadie pudo escribir
   el test que habría detectado §1.2. Extraer un puerto no fue solo limpieza: fue lo que hizo visible
   el fallo.
6. **"Compila" no significa "arranca".** Dos fallos aparecieron solo al ejecutar la app: un tipo de
   opciones no registrado en DI y un puerto sin registrar bajo la interfaz. Ninguno lo habría visto un
   test unitario.
7. **Un subagente que reporta "todo verde" puede tener cifras equivocadas.** Varias veces sus números no
   cuadraron con el código (confianza, umbral, aritmética de pesos) y una vez detectó un comentario
   mío que afirmaba algo falso. La verificación independiente no es opcional.
8. **La aritmética de las specs envejece.** Dos cifras de §4.5 (`0.98`, `0.72`) son inalcanzables con
   los pesos que la propia spec define. Cuando un número de ejemplo se calcula con supuestos que luego
   cambian, el ejemplo pasa a mentir en silencio. **Los tests afirman el código real**; la spec debería
   corregirse.

---

## Fuera de alcance

- Implementar cualquiera de estos puntos: requiere su propio ciclo brainstorming → spec → plan →
  implementing.
- Adaptadores `bank.*` por entidad financiera: el puerto `IBankAdapter` existe y está registrado, y son
  aditivos.
- Proveedores distintos de Gmail: el puerto `IEmailProviderClient` está listo; falta la implementación.
- Detección de cuotas, conversión de divisas, buzón dedicado + webhook entrante, backfill histórico,
  notificaciones de "se importaron N".

---

## Cómo usar este documento

1. **§1** — ya corregido. Se conserva para que no se repita el patrón, no como tarea.
2. **§2** — lo primero que atacaría si el usuario reporta fricción. §2.1 y §2.2 son baratas y visibles;
   §2.3 necesita una decisión de producto antes de escribir código.
3. **§3** — deuda de convención. §3.1 es la única que toca Finanzas; hacerlo bien requiere revisar
   los `catch` existentes que dependan del tipo concreto.
4. **§5.1** — la prueba de humo debería hacerse antes de que el primer usuario real toque el módulo.
5. **§8** — leer antes de "optimizar" o "simplificar": ahí está lo que funciona y por qué.
