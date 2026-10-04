import { expect, test, type Page } from "@playwright/test"
import { seedLoggedIn } from "./fixtures"
import { mockApi } from "./mocks/api"
import { EmailProvider } from "@/modules/email-import/types"

/**
 * E2E del módulo de importación por correo (slice 15 del plan 03-email-import).
 *
 * Sigue el patrón de `import.spec.ts`: mocks con `mockApi(page)` sobre `page.route`
 * y `seedLoggedIn` para el estado de sesión. No hay servidor real.
 *
 * Selectores por rol / nombre accesible, nunca por clase CSS. Los textos salen de
 * `i18n/locales/en.ts` (la suite arranca en inglés).
 *
 * Contrato (fiel al backend): los enums viajan como NÚMEROS (`provider: 0`,
 * `transactionType: 1` = Expense), `fieldConfidenceJson` es un STRING con el
 * diccionario serializado, y los errores de ErrorOr llegan como
 * `{ "Codigo.Error": ["descripcion"] }`.
 */

/**
 * `mockAccounts[0].id` NO es un GUID válido ("a1-1111-…") y el formulario valida
 * `GUID_RE` sobre `accountId`, así que con los ids de la fixture el guardado nunca
 * pasa la validación. Se declara una cuenta propia con GUID real.
 */
const ACCOUNT = {
  id: "11111111-2222-3333-4444-555555555555",
  name: "Main Checking",
  balance: 3200.5,
  currency: "USD",
  type: 0,
  color: "#4f46e5",
  isArchived: false,
}

/** Texto del aviso de moneda (§2.4), en una constante para que aserciones y mensajes no diverjan. */
const CURRENCY_WARNING = "The email's currency symbol could not be matched";

const CONNECTED = {
  provider: 0,
  email: "usuario@gmail.com",
  syncEnabled: true,
  autoCommitMinConfidence: 0.85,
  // Timestamp con Z a propósito (plan 16.3): el módulo muestra fechas con
  // parseDbDate (naive, sin zona), de modo que la Z se ignora y se muestra la
  // hora tal como la guardó el backend. Un `new Date(...Z)` la convertiría a la
  // zona local y divergiría en cualquier host que no sea UTC.
  lastSyncAt: "2024-05-01T10:00:00Z",
  lastError: null,
  lastSkippedCount: 2,
}

const SYNC_OK = {
  status: 200,
  body: {
  connectionsProcessed: 1,
  messagesFetched: 5,
  autoCommitted: 2,
  staged: 3,
  skipped: 0,
  failed: 0,
  connectionErrors: [] as string[],
  },
}

interface EmailTestState {
  connections: unknown[]
  rules: Record<string, unknown>[]
  inbox: Record<string, unknown>[]
  preview: unknown
  /** Respuesta cruda de /sync: el objeto completo del handler, con status y body. */
  sync: { status?: number; body: unknown }
  lastApprove: { id: string; body: Record<string, unknown> } | null
  lastDismiss: string | null
  lastBatch: Record<string, unknown> | null
  /** Último PUT /connections/{provider}/threshold recibido (slice 1). */
  lastThreshold: { provider: string; body: Record<string, unknown> } | null
  /** Si el PUT del umbral debe fallar, para ejercitar el rollback del slider. */
  thresholdError: { status: number; body: unknown } | null
  /** Si el preview del probador debe fallar, para probar los errores de mutación. */
  previewError: { status: number; body: unknown } | null
}

function makeState(over: Partial<EmailTestState> = {}): EmailTestState {
  return {
    connections: [],
    rules: [],
    inbox: [],
    preview: null,
    sync: SYNC_OK,
    lastThreshold: null,
    thresholdError: null,
    previewError: null,
    ...over,
  }
}

function makePending(over: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    id: "pend-1",
    connectionId: "conn-1",
    ruleId: "rule-x",
    subject: "Card purchase notification",
    merchant: "ACME Store",
    notes: null,
    amount: 42.5,
    currency: "EUR",
    exchangeRate: 1,
    date: "2024-01-15T00:00:00",
    transactionType: 1,
    confidence: 0.9,
    fieldConfidenceJson: '{"amount":0.9,"date":1,"merchant":1,"direction":1,"currency":1}',
    rawBody: null,
    failureReason: null,
    // Con Z a propósito (plan 16.3): la fila muestra createdAt con parseDbDate,
    // así que la Z se ignora. Regla contra la regresión a `new Date(...Z)`.
    createdAt: "2024-01-15T10:31:00Z",
    ...over,
  }
}

function makeInbox(n: number): Record<string, unknown>[] {
  return Array.from({ length: n }, (_, i) =>
    makePending({
      id: `pend-${i}`,
      confidence: 0.9,
      rawBody: i === 0 ? "RAW BODY MARKER 12345" : null,
    }),
  )
}

/** Mockea todas las rutas de `/api/email-import` desde el estado mutable del test. */
function setupEmailMocks(page: Page, state: EmailTestState) {
  const m = mockApi(page)

  // La cuenta con GUID real para el formulario de reglas (ver ACCOUNT).
  m.handle("GET", "/api/finances/accounts", [ACCOUNT])

  m.handle("GET", "/api/email-import/connections", () => ({ body: state.connections }))
  m.handle("POST", "/api/email-import/connections/authorize", () => ({
    body: { uri: "https://accounts.example.com/oauth/authorize?client_id=x&state=abc", state: "abc" },
  }))
  m.handle("DELETE", "/api/email-import/connections/*", () => ({ status: 204, body: null }))

  // Slice 1: el PUT del umbral. El patrón se registra DESPUÉS del DELETE porque los handlers
  // nuevos se anteponen (`unshift`) y un "*" voraz se tragaría "/gmail/threshold".
  m.handle("PUT", "/api/email-import/connections/*/threshold", ({ request, url }) => {
    // path = /api/email-import/connections/{provider}/threshold → provider en el índice 4
    const provider = url.pathname.split("/")[4]

    // El harness imita el binder JSON de Minimal API: sin `application/json` responde 415, como
    // haría la API real. Sin esta comprobación, el PUT que viajaba sin Content-Type pasaba el
    // test y habría fallado en producción — el stub perdonando el defecto (§9.1 de la spec).
    const contentType = request.headers()["content-type"] ?? ""
    if (!contentType.includes("application/json")) return { status: 415, body: null }

    // El frontend manda el enum numérico (EmailProvider.Gmail === 0); la aserción usa el
    // nombre legible, así que se compara contra lo que realmente viaja por la ruta.
    // `request` es el MockRequest del harness, que expone `postDataJSON()` ya parseado;
    // el request de Playwright no tiene postData().
    state.lastThreshold = { provider, body: request.postDataJSON() ?? {} }
    if (state.thresholdError) return state.thresholdError
    // El servidor es la autoridad: refleja el valor guardado para que el siguiente GET lo
    // devuelva. Se REEMPLAZA la entrada, no se muta: CONNECTED es un objeto de módulo y
    // mutarlo filtraría el cambio al resto de tests del worker (el día que otro dispare un PUT).
    state.connections = state.connections.map((c, i) =>
      i === 0
        ? {
            ...(c as Record<string, unknown>),
            autoCommitMinConfidence: Number(state.lastThreshold!.body.threshold),
          }
        : c,
    )
    return { status: 204, body: null }
  })

  m.handle("POST", "/api/email-import/rules/preview", () =>
    state.previewError ? state.previewError : { body: state.preview },
  )
  m.handle("GET", "/api/email-import/rules", () => ({ body: state.rules }))
  m.handle("POST", "/api/email-import/rules", ({ request }) => {
    const body = request.postDataJSON() as Record<string, unknown>
    const id = `rule-${state.rules.length + 1}`
    state.rules.push({ id, ...body })
    return { body: { id } }
  })
  m.handle("PUT", "/api/email-import/rules/*", () => ({ status: 204, body: null }))
  m.handle("DELETE", "/api/email-import/rules/*", () => ({ status: 204, body: null }))

  m.handle("GET", "/api/email-import/inbox", () => ({
    body: { items: state.inbox, total: state.inbox.length, page: 1, pageSize: 50 },
  }))
  m.handle("POST", "/api/email-import/inbox/approve-batch", ({ request }) => {
    state.lastBatch = request.postDataJSON() as Record<string, unknown>
    return { body: { approved: 2, failed: 0, failures: [] } }
  })
  m.handle("POST", "/api/email-import/inbox/*/approve", ({ request, url }) => {
    const id = url.pathname.split("/").filter(Boolean).at(-2) as string
    state.lastApprove = { id, body: request.postDataJSON() as Record<string, unknown> }
    state.inbox = state.inbox.filter((i) => i.id !== id)
    return { body: { id } }
  })
  m.handle("POST", "/api/email-import/inbox/*/dismiss", ({ url }) => {
    state.lastDismiss = url.pathname.split("/").filter(Boolean).at(-2) as string
    state.inbox = state.inbox.filter((i) => i.id !== state.lastDismiss)
    return { status: 204, body: null }
  })

  m.handle("POST", "/api/email-import/sync", () => state.sync)

  return m
}

async function openRulesTab(page: Page) {
  await page.goto("/email-import")
  await page.getByRole("tab", { name: /Rules/ }).click()
}

test.describe("email import", () => {
  test("navega desde el sidebar y muestra las dos pestañas", async ({ page }) => {
    await seedLoggedIn(page)
    setupEmailMocks(page, makeState({ connections: [CONNECTED] }))

    await page.goto("/dashboard")
    await page.getByRole("link", { name: "Email" }).click()

    await expect(page.getByRole("tab", { name: /Pending/ })).toBeVisible()
    await expect(page.getByRole("tab", { name: /Rules/ })).toBeVisible()
  })

  test("conecta el buzón: navega a la uri del proveedor y refleja el retorno", async ({ page }) => {
    const state = makeState()
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    // Intercepta la navegación al proveedor OAuth para no salir de la suite.
    let oauthUrl: string | null = null
    await page.route("**/accounts.example.com/**", (route) => {
      oauthUrl = route.request().url()
      return route.fulfill({ status: 200, contentType: "text/html", body: "<html></html>" })
    })

    await page.goto("/settings")
    await page.getByRole("button", { name: /Connect/ }).first().click()
    await page.waitForURL(/accounts\.example\.com/)
    expect(oauthUrl).toContain("accounts.example.com")

    // Retorno del callback: el backend redirige con ?emailImport=connected.
    state.connections = [CONNECTED]
    await page.goto("/settings?emailImport=connected")

    await expect(page.getByText("usuario@gmail.com")).toBeVisible()
  })

  test("crea una regla con sus patrones y aparece en la lista", async ({ page }) => {
    const state = makeState({ connections: [CONNECTED] })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await openRulesTab(page)
    await page.getByRole("button", { name: "New rule" }).click()

    await page.getByLabel("Name", { exact: true }).fill("Card purchases")
    // El Select de Base UI expone role=combobox; se identifica por su texto visible.
    // El combobox de cuenta está asociado por htmlFor a su FieldLabel, así que se
    // localiza por etiqueta accesible y no por el texto del placeholder.
    await page.getByLabel("Target account").click()
    await page.getByRole("option", { name: ACCOUNT.name }).click()

    await page.locator("#rule-from").fill("notifications@bank.com")
    await page.getByLabel("Subject contains").fill("Card purchase")
    await page.getByLabel(/Body contains/).fill("Purchase of")
    await page.getByLabel(/Amount pattern/).fill("Amount:\\s*([\\d.,]+)")
    await page.getByLabel(/Date pattern/).fill("\\d{2}/\\d{2}/\\d{4}")
    await page.getByLabel(/Merchant pattern/).fill("at\\s+([A-Z][\\w]+)")
    await page.getByLabel(/Sign pattern/).fill("(income|credit)")
    await page.getByLabel(/Currency symbol pattern/).fill("(EUR|€)")

    await page.getByRole("button", { name: /Save/ }).click()

    await expect(page.getByText("Card purchases")).toBeVisible()
    expect(state.rules).toHaveLength(1)
    expect(state.rules[0].accountId).toBe(ACCOUNT.id)
    // Aserción sobre el PATRÓN CAPTURADO, no sobre la clave: `buildPatternsJson` serializa
// "amount" siempre, aunque valga null, así que `toContain("amount")` era cierto incluso con
    // los cinco patrones vacíos — un test que no podía fallar (§9.3 de la spec).
    expect(state.rules[0].fieldPatternsJson).toContain("Amount:")
  })

  test("prueba una regla y muestra texto plano, campos y explicación literal", async ({ page }) => {
    const state = makeState({
      connections: [CONNECTED],
      preview: {
        plainText: "Subject: Receipt\nAmount: 12.50 EUR\nMerchant: ACME",
        fields: { amount: "12.50", currency: "EUR", merchant: "ACME", date: "2024-01-01" },
        fieldConfidence: { amount: 1, currency: 1, merchant: 1, date: 1, direction: 1 },
        confidence: 1,
        explanation: ["amount: 1.00 — anchored on the first match", "date: 1.00 — parsed"],
        skipReason: null,
      },
    })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await openRulesTab(page)
    await page.getByRole("button", { name: "New rule" }).click()
    await page.getByLabel("Name", { exact: true }).fill("Card purchases")
    // El combobox de cuenta está asociado por htmlFor a su FieldLabel, así que se
    // localiza por etiqueta accesible y no por el texto del placeholder.
    await page.getByLabel("Target account").click()
    await page.getByRole("option", { name: ACCOUNT.name }).click()
    await page.getByLabel(/Amount pattern/).fill("Amount:\\s*([\\d.,]+)")

    // El botón Test solo se habilita con un origen de regla elegido: el borrador del
    // formulario. "Form draft" aparece en la lista cuando el borrador es testeable
    // (nombre + cuenta GUID + patrón de importe), pero el trigger sigue mostrando el
    // placeholder hasta que se elige explícitamente.
    //
    // Slice 15: el Select del probador está asociado por htmlFor a su FieldLabel,
    // así que se localiza por etiqueta accesible. El filtro por texto de antes
    // ("/Select a rule or open the form|Form draft/") dejó de ser fiable al
    // asociarse bien: el nombre accesible es la etiqueta, no el placeholder.
    const testerRuleSelect = page.getByLabel("Rule to test")
    await testerRuleSelect.click()
    await page.getByRole("option", { name: "Form draft" }).click()
    await expect(testerRuleSelect).toContainText("Form draft")

    await page.locator("#tester-raw").fill(
      "Subject: Receipt\nAmount: 12.50 EUR\nMerchant: ACME",
    )
    await page.getByRole("button", { name: "Test" }).click()

    // Texto plano del conversor (la parte que revela que el HTML no parseó bien).
    await expect(page.getByText(/Subject: Receipt/).first()).toBeVisible()
    // Explicación literal devuelta por el backend.
    await expect(page.getByText(/anchored on the first match/)).toBeVisible()
    // Slice 15.6: el resultado asíncrono se anuncia con role="status" (polite).
    // Aserción estricta: si el bloque no llevara role="status", getByRole no
    // resuelve ningún nodo y el test falla.
    await expect(page.getByRole("status").filter({ hasText: /Total confidence/ })).toBeVisible()
  })

  test("sincroniza y actualiza el contador de pendientes", async ({ page }) => {
    const state = makeState({ connections: [CONNECTED], inbox: makeInbox(5) })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await page.goto("/email-import")
    await expect(page.getByRole("tab", { name: /Pending/ })).toContainText("5")

    state.inbox = makeInbox(2)
    await page.goto("/settings")
    await page.getByRole("button", { name: /Sync now/ }).click()

    await page.goto("/email-import")
    await expect(page.getByRole("tab", { name: /Pending/ })).toContainText("2")
  })

  test("revisa un pendiente de baja confianza: badges con texto, corrige y aprueba", async ({ page }) => {
    const state = makeState({
      connections: [CONNECTED],
      inbox: [
        makePending({
          id: "pend-1",
          confidence: 0.3,
          fieldConfidenceJson: '{"amount":0.3,"date":1,"merchant":1,"direction":1,"currency":1}',
        }),
      ],
    })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await page.goto("/email-import")

    // Badges por campo: texto + porcentaje, NUNCA solo color.
    const badges = page.getByRole("list", { name: "Confidence per field" })
    await expect(badges).toBeVisible()
    await expect(badges.getByRole("listitem").filter({ hasText: "Amount" })).toContainText("30%")
    await expect(badges.getByRole("listitem").filter({ hasText: "Date" })).toContainText("100%")

    // El id del input de importe deriva del id de la fila (amount-<id>).
    await page.locator("#amount-pend-1").fill("12.34")
    await page.getByRole("button", { name: "Approve", exact: true }).first().click()

    await expect.poll(() => state.lastApprove).not.toBeNull()
    expect(state.lastApprove!.id).toBe("pend-1")
    expect(state.lastApprove!.body.amount).toBe(12.34)

    // La fila desaparece de la lista.
    await expect(page.locator("#amount-pend-1")).toHaveCount(0)
  })

  test("exige el patrón de importe con un mensaje i18n, no la clave cruda", async ({ page }) => {
    const state = makeState({ connections: [CONNECTED] })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await openRulesTab(page)
    await page.getByRole("button", { name: "New rule" }).click()
    await page.getByLabel("Name", { exact: true }).fill("Sin patrón")
    await page.getByLabel("Target account").click()
    await page.getByRole("option", { name: ACCOUNT.name }).click()

    // Sin patrón de importe: el backend lo rechazaría, así que el cliente debe avisar
    // ANTES. Si la clave i18n faltara, i18next muestra "emailImport.rules.patternRequired".
    await page.getByRole("button", { name: "Save rule" }).click()

    await expect(page.getByText("The amount pattern is required.")).toBeVisible()
    await expect(page.getByText("emailImport.rules.patternRequired")).toHaveCount(0)
    expect(state.rules).toHaveLength(0)
  })

  test("despliega el correo original", async ({ page }) => {
    const state = makeState({ connections: [CONNECTED], inbox: makeInbox(1) })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await page.goto("/email-import")
    await page.getByRole("button", { name: "View original email" }).first().click()
    await expect(page.getByText("RAW BODY MARKER 12345")).toBeVisible()
  })

  test("descarta un pendiente y ofrece guardar como regla con el formulario prellenado", async ({ page }) => {
    const state = makeState({
      connections: [CONNECTED],
      inbox: [
        makePending({
          id: "pend-7",
          subject: "Coffee subscription",
          rawBody: "From: shop@example.com\nSubject: Coffee subscription\nAmount: 9.99 EUR",
        }),
      ],
    })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await page.goto("/email-import")
    await page.getByRole("button", { name: "Dismiss", exact: true }).first().click()

    await expect(page.getByRole("dialog")).toBeVisible()
    await page.getByRole("dialog").getByRole("button", { name: /rule/i }).click()

    await expect(page.getByLabel("Name", { exact: true })).toHaveValue("Coffee subscription")
    await expect(page.locator("#rule-from")).toHaveValue("shop@example.com")
  })

  test("aprueba en lote los de alta confianza con confirmación previa", async ({ page }) => {
    const state = makeState({ connections: [CONNECTED], inbox: makeInbox(3) })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await page.goto("/email-import")
    await page.getByRole("button", { name: "Approve high-confidence ones" }).click()

    const dialog = page.getByRole("alertdialog")
    await expect(dialog).toBeVisible()
    await expect(dialog.getByText(/Approve high-confidence emails\?/)).toBeVisible()
    await dialog.getByRole("button", { name: "Approve", exact: true }).click()

    await expect.poll(() => state.lastBatch).not.toBeNull()
    expect(state.lastBatch!.minConfidence).toBe(0.85)
    expect(state.lastBatch!.take).toBe(100)
  })

  test("el 400 SyncAlreadyScheduled se muestra como aviso, no como error genérico", async ({ page }) => {
    const state = makeState({
      connections: [CONNECTED],
      sync: {
        status: 400,
        body: { "EmailImport.SyncAlreadyScheduled": ["The sync worker is already scheduled."] },
      },
    })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await page.goto("/settings")
    await page.getByRole("button", { name: /Sync now/ }).click()

    // Scope al toast con el atributo de sonner: el badge "Automatic sync" de la tarjeta
    // también contiene "automatic" y va antes en el DOM, así que un getByText global
    // matchearía el badge y el test pasaría aunque el aviso no saliera.
    // (sonner NO expone role="status" en estos toasts: verificado, 0 nodos.)
    const toast = page.locator("[data-sonner-toast]").filter({ hasText: /Sync is automatic/ })
    await expect(toast).toBeVisible()
    await expect(page.getByText("Could not sync")).toHaveCount(0)
  })

  // ── Slice 16.3: timestamps con Z (plan 04-email-import-debt) ────────────────
  // Fixtures con Z + zona horaria fija: la convención del módulo es parseDbDate
  // (naive, sin zona), así que la Z se ignora y se muestra la hora del backend.
  // `new Date(...Z)` convertiría a la zona local y, en un host UTC, las dos
  // convenciones darían el mismo resultado: sin timezoneId este test pasaría con
  // cualquiera de las dos y no probaría nada (lección §9.2 de la spec).
  test.describe("timestamps con Z", () => {
    test.use({ timezoneId: "Europe/Madrid" })

    test("muestra lastSyncAt y createdAt con Z como los guardó el backend", async ({ page }) => {
      const state = makeState({ connections: [CONNECTED], inbox: [makePending({ id: "pend-z" })] })
      await seedLoggedIn(page)
      setupEmailMocks(page, state)

      // Tarjeta de Ajustes: lastSyncAt = 2024-05-01T10:00:00Z. Madrid en mayo es
      // UTC+2: `new Date(...Z)` mostraría 12:00 PM; parseDbDate muestra 10:00 AM.
      // (date-fns v4 con locale en-US separa fecha y hora con coma: "MMM d, yyyy, h:mm a".)
      await page.goto("/settings")
      await expect(page.getByText("May 1, 2024, 10:00 AM")).toBeVisible()
      // Aserción negativa estricta: si la tarjeta volviera a convertir el Z a la
      // zona local, el texto UTC-shifted aparecería y el test fallaría.
      await expect(page.getByText("May 1, 2024, 12:00 PM")).toHaveCount(0)

      // Bandeja: createdAt = 2024-01-15T10:31:00Z en la fila. Madrid en enero es
      // UTC+1: la regresión a `new Date(...Z)` mostraría 11:31 AM.
      await page.goto("/email-import")
      await expect(page.getByText("Jan 15, 2024, 10:31 AM")).toBeVisible()
      await expect(page.getByText("Jan 15, 2024, 11:31 AM")).toHaveCount(0)
    })
  })

  // ── Slice 16.2: importes con separador de miles (plan 04-email-import-debt) ──
  // El parser anterior hacía `Number(amount.replace(",", "."))` (sin `g`): solo
  // sustituía la primera coma y cualquier formato con miles devolvía NaN, que
  // deshabilita el botón y no llega a la API. Cada formato debe aprobar con el
  // número correcto; si el parser falla, lastApprove sigue en null y el poll
  // expira → el test falla cuando lo que dice su nombre no ocurre.
  const AMOUNT_CASES: Array<{ label: string; input: string; expected: number }> = [
    { label: "es-ES con miles y decimal con coma", input: "1.234,56", expected: 1234.56 },
    { label: "en-US con miles y decimal con punto", input: "1,234.56", expected: 1234.56 },
    { label: "dos comas (el caso NaN del plan 16.2)", input: "1,234,56", expected: 1234.56 },

    // La ambigüedad documentada se fija aquí a propósito: si alguien invirtiera la lectura,
    // el factor ×1000 pasaría sin romper ningún otro caso.
    {
      label: "un solo separador con tres dígitos (ambigüedad documentada)",
      input: "1.234",
      expected: 1.234,
    },
    // `1.234,` se normalizaba antes a 1234 (×1000) porque el separador final se tomaba
    // como decimal de una parte vacía.
    { label: "separador colgante (no debe multiplicar por mil)", input: "1.234,", expected: 1.234 },
  ]

  // Entradas que NO son un importe escrito a mano. Antes pasaban el filtro: `0x10` era 16 y
  // `1e5` era 100000, ambos finitos y > 0, así que el botón quedaba habilitado y se
  // aprobaba un importe inventado. Ahora el botón se queda deshabilitado.
  const AMOUNT_REJECTED: Array<{ label: string; input: string }> = [
    { label: "hexadecimal", input: "0x10" },
    { label: "notación científica", input: "1e5" },
    { label: "sólo separadores", input: ",." },
  ]

  for (const { label, input, expected } of AMOUNT_CASES) {
    test(`aprueba un importe en formato ${label}`, async ({ page }) => {
      const state = makeState({
        connections: [CONNECTED],
        inbox: [makePending({ id: "pend-amt" })],
      })
      await seedLoggedIn(page)
      setupEmailMocks(page, state)

      await page.goto("/email-import")
      await page.locator("#amount-pend-amt").fill(input)
      await page.getByRole("button", { name: "Approve", exact: true }).first().click()

      await expect.poll(() => state.lastApprove).not.toBeNull()
      expect(state.lastApprove!.id).toBe("pend-amt")
      // Aserción estricta sobre el payload: no basta con que el clic llegue.
      expect(state.lastApprove!.body.amount).toBe(expected)
    })
  }

  // Aserción negativa: entradas que NO son importes no pueden llegar a la API. El botón
  // tiene que quedarse deshabilitado; si se enviara, se aprobaría un importe inventado.
  for (const { label, input } of AMOUNT_REJECTED) {
    test(`no aprueba un importe en formato ${label}`, async ({ page }) => {
      const state = makeState({
        connections: [CONNECTED],
        inbox: [makePending({ id: "pend-bad" })],
      })
      await seedLoggedIn(page)
      setupEmailMocks(page, state)

      await page.goto("/email-import")
      await page.locator("#amount-pend-bad").fill(input)

      await expect(
        page.getByRole("button", { name: "Approve", exact: true }).first(),
      ).toBeDisabled()
      // El mock nunca recibió un approve. `lastApprove` arranca en `null` en makeState, pero
      // se afirma la ausencia (`falsy`) y no `toBeNull()` porque el tipo lo declara opcional.
      expect(state.lastApprove).toBeFalsy()
    })
  }

  // Antes el slider era `disabled` porque el backend no exponía su actualización:
  // un parámetro que parecía configurable y no lo era. La aserción mira el PUT
  // recibido, no que el número de la pantalla cambie — si el valor se moviera en
  // local sin persistir, la UI "funcionaría" y el usuario perdería el ajuste al
  // recargar.
  test("guarda el umbral de auto-commit con un PUT al backend", async ({ page }) => {
    const state = makeState({ connections: [{ ...CONNECTED }] })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await page.goto("/settings")
    // El primitive de Base UI expone role="group" + aria-label en la raíz y delega el rol
    // "slider" al <input type="range"> interno. Por eso el locatable es la raíz etiquetada.
    const slider = page.getByRole("group", { name: "Auto-commit threshold" })
    await expect(slider).toBeEnabled()

    const thumb = slider.locator('input[type="range"]')

    // El valor del servidor debe estar en pantalla ANTES de interactuar. Sin esta aserción el
    // test pasaría aunque el slider naciera en 0: el guard de sincronización no puede absorber
    // el primer dato (ambos estados nacen en 0), y entonces el primer arrastre se revierte solo
    // y `onValueCommitted` cortocircuita por ser igual al valor del servidor, sin mandar PUT.
    await expect(thumb).toHaveValue("0.85")
    await expect(page.getByText("85%", { exact: true })).toBeVisible()

    await thumb.fill("0.93")
    await thumb.blur()

    await expect.poll(() => state.lastThreshold).not.toBeNull()
    expect(state.lastThreshold!.provider).toBe(String(EmailProvider.Gmail))
    expect(state.lastThreshold!.body.threshold).toBeCloseTo(0.93, 2)
  })

  test("revierte el umbral al valor del servidor cuando el PUT falla", async ({ page }) => {
    const state = makeState({
      connections: [{ ...CONNECTED }],
      thresholdError: { status: 400, body: { EmailImportInvalidThreshold: ["fuera de rango"] } },
    })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await page.goto("/settings")
    const slider = page.getByRole("group", { name: "Auto-commit threshold" })
    const thumb = slider.locator('input[type="range"]')
    await thumb.fill("0.4")
    await thumb.blur()

    // El servidor es la autoridad: si rechaza el valor, el slider no debe seguir
    // enseñando un umbral que no está guardado.
    await expect(page.getByRole("alert")).toContainText("The auto-commit threshold could not be saved")
    await expect.poll(async () => await thumb.inputValue()).toBe("0.85")
  })

  // ── Slice 4: aviso destacado de moneda ──
  // El dato viajaba sólo como una línea más del Explanation del backend, visible
  // únicamente al abrir el probador. Ahora hay un aviso en la fila de revisión con
  // el MISMO texto (mismas claves i18n): un mensaje en una sola de las dos
  // superficies es la mitad del problema.
  test("avisa en la fila de revisión cuando el símbolo de moneda no se pudo identificar", async ({
    page,
  }) => {
    const state = makeState({
      connections: [CONNECTED],
      inbox: [
        makePending({
          id: "pend-cur",
          // currency: 0 = símbolo no identificado (¥, CHF…). NO es la contradicción:
          // desde el slice 3 esa nunca llega a la bandeja.
          fieldConfidenceJson: '{"amount":1,"date":1,"merchant":1,"direction":1,"currency":0}',
        }),
      ],
    })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await page.goto("/email-import")

    await expect(page.getByText(CURRENCY_WARNING, { exact: false })).toBeVisible()
  })

  test("no muestra el aviso de moneda cuando la confianza es correcta", async ({ page }) => {
    const state = makeState({
      connections: [CONNECTED],
      inbox: [makePending({ id: "pend-ok" })], // fieldConfidenceJson con currency: 1
    })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await page.goto("/email-import")

    // Aserción negativa: el aviso sólo debe existir cuando currency == 0. Sin esto, un
    // componente que se renderiza siempre pasaría el test positivo.
    await expect(page.getByText(CURRENCY_WARNING, { exact: false })).toHaveCount(0)
  })

  // ── Slice 15: selectores accesibles ──
  // El combobox del probador se localiza por etiqueta, no por el texto de su placeholder. Con
  // el filtro por texto anterior bastaba con que el componente existiera; ahora el test falla si
  // la asociación `htmlFor`/`id` se rompe, que es justo lo que había que proteger (§7.15.4).
  test("el probador se localiza por su etiqueta accesible", async ({ page }) => {
    const state = makeState({
      rules: [
        { id: "rule-a", name: "Santander", adapterKey: "generic.regex", priority: 1, isEnabled: true },
      ],
      inbox: [makePending({ id: "pend-x" })],
      preview: {
        matched: true,
        amount: "12.50",
        currency: "EUR",
        transactionType: 1,
        plainText: "Amount: 12.50 EUR",
        fieldConfidence: { amount: 1, currency: 1, merchant: 1, date: 1, direction: 1 },
        confidence: 1,
        explanation: ["amount: 1.00 — anchored on the first match"],
        skipReason: null,
      },
    })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await page.goto("/email-import")
    await openRulesTab(page)

    const ruleSelect = page.getByLabel("Rule to test")
    await expect(ruleSelect).toBeVisible()
  })

  // El probador anunciaba sus resultados sin role="status": una región que aparece por
  // mutación y no se anuncia es invisible para un lector de pantalla (plan §7.15.6).
  test("el resultado del probador se anuncia como estado", async ({ page }) => {
    const state = makeState({
      rules: [
        { id: "rule-a", name: "Santander", adapterKey: "generic.regex", priority: 1, isEnabled: true },
      ],
      inbox: [makePending({ id: "pend-x" })],
      preview: {
        matched: true,
        amount: "12.50",
        currency: "EUR",
        transactionType: 1,
        plainText: "Amount: 12.50 EUR",
        fieldConfidence: { amount: 1, currency: 1, merchant: 1, date: 1, direction: 1 },
        confidence: 1,
        explanation: ["amount: 1.00 — anchored on the first match"],
        skipReason: null,
      },
    })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await page.goto("/email-import")
    await openRulesTab(page)

    await page.getByLabel("Rule to test").click()
    await page.getByRole("option", { name: /Santander/ }).click()
    await page.locator("#tester-raw").fill("Amount: 12.50 EUR")
    await page.getByRole("button", { name: "Test", exact: true }).click()

    // Acotado por CONTENIDO, no por el mero atributo: `locator('[role="status"]')` resolvería
    // con cualquier región viva de la página y pasaría aunque el bloque del probador no se
    // anunciara (plan deuda §9.2: una aserción floja oculta defectos).
    await expect(
      page.getByRole("status").filter({ hasText: /Total confidence/ }),
    ).toBeVisible()
  })

  // El probador conservaba el resultado de la regla ANTERIOR al cambiar de selección: `setResult`
  // sólo se llamaba en onSuccess, así que se mostraba el desglose de confianza de otra regla bajo
  // el nombre de la nueva (§7.18.2).
  test("el probador no conserva el resultado de la regla anterior", async ({ page }) => {
    const state = makeState({
      rules: [
        { id: "rule-a", name: "Santander", adapterKey: "generic.regex", priority: 1, isEnabled: true },
        { id: "rule-b", name: "BBVA", adapterKey: "generic.regex", priority: 2, isEnabled: true },
      ],
      inbox: [makePending({ id: "pend-x" })],
      // `fields` es lo que el bloque de resultado pinta como lista de badges; sin él el
      // bloque cae en la rama "no fields" y el importe no aparece (el test debe fallar
      // cuando lo que dice su nombre no ocurre, no por un fixture incompleto).
      preview: {
        matched: true,
        amount: "99.99",
        currency: "EUR",
        transactionType: 1,
        plainText: "Amount: 99.99 EUR",
        fields: { amount: "99.99", currency: "EUR", merchant: "ACME", date: "2024-01-01" },
        fieldConfidence: { amount: 1, currency: 1, merchant: 1, date: 1, direction: 1 },
        confidence: 1,
        explanation: ["amount: 1.00 — anchored on the first match"],
        skipReason: null,
      },
    })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await page.goto("/email-import")
    await openRulesTab(page)

    await page.getByLabel("Rule to test").click()
    await page.getByRole("option", { name: /Santander/ }).click()
    await page.locator("#tester-raw").fill("Amount: 99.99 EUR")
    await page.getByRole("button", { name: "Test", exact: true }).click()

    // Aserción sobre el bloque de resultado, no sobre el texto suelto: el `99.99` también
    // está en el textarea del correo crudo, y buscarlo en la página entera daría un falso
    // positivo permanente (y un test que nunca fallaría).
    const resultBlock = page.getByRole("status").filter({ hasText: "99.99" })
    await expect(resultBlock).toHaveCount(1)

    // Cambiar de regla debe limpiar el resultado: si persiste, el usuario lee un desglose
    // que corresponde a otra regla y lo aprueba creyendo que es el de la que tiene delante.
    await page.getByLabel("Rule to test").click()
    await page.getByRole("option", { name: /BBVA/ }).click()

    await expect(resultBlock).toHaveCount(0)
  })

  // El aviso de moneda también debe existir en el PROBADOR, con el mismo texto que en la fila
  // de revisión: un mensaje en una sola superficie es la mitad del problema (§2.4).
  test("el probador también avisa cuando no identifica el símbolo de moneda", async ({ page }) => {
    const state = makeState({
      rules: [
        { id: "rule-a", name: "Santander", adapterKey: "generic.regex", priority: 1, isEnabled: true },
      ],
      inbox: [makePending({ id: "pend-x" })],
      preview: {
        matched: true,
        amount: "12.50",
        currency: "EUR",
        transactionType: 1,
        plainText: "Amount: 12.50 CHF",
        fieldConfidence: { amount: 1, currency: 0, merchant: 1, date: 1, direction: 1 },
        confidence: 0.9,
        explanation: ["currency: 0.00 — captured symbol contradicts the rule currency"],
        skipReason: null,
      },
    })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await page.goto("/email-import")
    await openRulesTab(page)

    await page.getByLabel("Rule to test").click()
    await page.getByRole("option", { name: /Santander/ }).click()
    await page.locator("#tester-raw").fill("Amount: 12.50 CHF")
    await page.getByRole("button", { name: "Test", exact: true }).click()

    await expect(page.getByText(CURRENCY_WARNING, { exact: false })).toBeVisible()
  })

  // ── Slice 16.1: el aviso de regex NO bloquea el guardado ──
  // La decisión (documentada en `lib/regex-hint.ts`) es deliberada: la sintaxis JS no es la de
  // .NET, así que hay regex válidas en .NET que el navegador rechaza (los modificadores inline
  // `(?n)`). Si el aviso bloquease, esas reglas serían inuardables. Este test la fija: si alguien
  // añade `disabled={hasRegexError}`, falla.
  test("una regex que el navegador no compila avisa pero NO bloquea el guardado", async ({
    page,
  }) => {
    const state = makeState()
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await openRulesTab(page)
    await page.getByRole("button", { name: "New rule" }).click()
    await page.getByLabel("Name", { exact: true }).fill("Con regex rota")
    await page.getByLabel("Target account").click()
    await page.getByRole("option", { name: ACCOUNT.name }).click()
    // Paréntesis sin cerrar: sintaxis inválida en JS.
    await page.getByLabel(/Amount pattern/).fill("Amount:(\\s*")

    // 1. El aviso aparece.
    await expect(page.getByText(/regular expression|expresión regular/i).first()).toBeVisible()

    // 2. Guardar sigue habilitado.
    const save = page.getByRole("button", { name: /Save/ }).first()
    await expect(save).toBeEnabled()

    // 3. Y el POST sale de verdad: el backend es el juez y responderá con el 400 real.
    await save.click()
    await expect.poll(() => state.rules.length).toBe(1)
    expect(state.rules[0].fieldPatternsJson).toContain("Amount:")
  })

  // ── Slice 15.4: el filtro de estado tiene etiqueta ──
  // Antes no había ningún elemento `<label>` en ese Select. El test falla si se rompe el
  // `htmlFor`/`id`, que es exactamente lo que había que proteger.
  test("el filtro de estado de la bandeja se localiza por su etiqueta", async ({ page }) => {
    const state = makeState({ connections: [CONNECTED], inbox: [makePending()] })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await page.goto("/email-import")

    const statusFilter = page.getByLabel("Status")
    await expect(statusFilter).toBeVisible()
  })

  // ── Slice 18.2 (I5): el resultado caduca al EDITAR el borrador ──
  // Limpiar el resultado al cambiar de REGLA no basta: con "Form draft" seleccionado, seguir
  // escribiendo en el formulario lo invalida igual, y el desglose viejo se quedaba en pantalla
  // bajo la MISMA etiqueta — con role="status" sin re-anuncio, porque el nodo no cambiaba.
  test("el probador descarta el resultado cuando se edita el borrador que lo produjo", async ({
    page,
  }) => {
    const state = makeState({
      preview: {
        matched: true,
        amount: "99.99",
        currency: "EUR",
        transactionType: 1,
        plainText: "Importe: 99.99 EUR",
        fields: { amount: "99.99", currency: "EUR" },
        fieldConfidence: { amount: 1, currency: 1, merchant: 1, date: 1, direction: 1 },
        confidence: 1,
        explanation: ["amount: 1.00 — anchored on the first match"],
        skipReason: null,
      },
      inbox: [makePending({ id: "pend-x" })],
    })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await openRulesTab(page)
    await page.getByRole("button", { name: "New rule" }).click()
    await page.getByLabel("Name", { exact: true }).fill("Con borrador")
    await page.getByLabel("Target account").click()
    await page.getByRole("option", { name: ACCOUNT.name }).click()
    await page.getByLabel(/Amount pattern/).fill("Importe:\\s*([\\d.,]+)")

    const ruleSelect = page.getByLabel("Rule to test")
    await ruleSelect.click()
    await page.getByRole("option", { name: "Form draft" }).click()

    await page.locator("#tester-raw").fill("Importe: 99.99 EUR")
    await page.getByRole("button", { name: "Test", exact: true }).click()

    const resultBlock = page.getByRole("status").filter({ hasText: "99.99" })
    await expect(resultBlock).toHaveCount(1)

    // Editar el patrón del borrador cambia lo que la regla extraería: el resultado ya no
    // describe lo que hay en pantalla y debe desaparecer.
    await page.getByLabel(/Amount pattern/).fill("Importe:\\s*([\\d.]+)")

    await expect(resultBlock).toHaveCount(0)
  })

  // ── Slice 18.2 (I6): el formulario NO se reabre al volver a la pestaña ──
  // Base UI desmontaba el panel inactivo, así que RulesTab se remontaba con la petición de
  // apertura sin consumir y el formulario volvía a abrirse solo con el prefill viejo.
  test("el formulario no se reabre al volver a la pestaña de reglas", async ({ page }) => {
    const state = makeState({
      connections: [CONNECTED],
      inbox: [makePending({ id: "pend-rule", subject: "Aviso de compra" })],
    })
    await seedLoggedIn(page)
    setupEmailMocks(page, state)

    await page.goto("/email-import")

    // Descartar el pendiente abre el prompt, que a su vez abre el formulario en la pestaña
    // de reglas. El prompt es un diálogo, así que el botón va dentro de él.
    await page.getByRole("button", { name: "Dismiss", exact: true }).first().click()
    await expect(page.getByRole("dialog")).toBeVisible()
    await page.getByRole("dialog").getByRole("button", { name: /rule/i }).click()

    await expect(page.getByLabel("Name", { exact: true })).toHaveValue("Aviso de compra")

    // Cerrarlo y ausentarse a la bandeja.
    await page.getByRole("button", { name: "Cancel" }).click()
    await expect(page.getByLabel("Name", { exact: true })).toHaveCount(0)

    await page.getByRole("tab", { name: "Pending" }).click()
    await page.getByRole("tab", { name: "Rules" }).click()

    // El prefill es de una sola vez: si el formulario reaparece aquí, la petición no se consumió.
    await expect(page.getByLabel("Name", { exact: true })).toHaveCount(0)
  })
})
