import { expect, test, type Page } from "@playwright/test"
import { seedLoggedIn } from "./fixtures"
import { mockApi } from "./mocks/api"

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

const CONNECTED = {
  provider: 0,
  email: "usuario@gmail.com",
  syncEnabled: true,
  autoCommitMinConfidence: 0.85,
  lastSyncAt: "2024-05-01T10:00:00",
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
}

function makeState(over: Partial<EmailTestState> = {}): EmailTestState {
  return { connections: [], rules: [], inbox: [], preview: null, sync: SYNC_OK, ...over }
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
    createdAt: "2024-01-15T10:31:00",
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

  m.handle("POST", "/api/email-import/rules/preview", () => ({ body: state.preview }))
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
    expect(state.rules[0].fieldPatternsJson).toContain("amount")
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
    const testerCombobox = page
      .getByRole("combobox")
      .filter({ hasText: /Select a rule or open the form|Form draft/ })
      .first()
    await testerCombobox.click()
    await page.getByRole("option", { name: "Form draft" }).click()
    await expect(testerCombobox).toContainText("Form draft")

    await page.locator("#tester-raw").fill(
      "Subject: Receipt\nAmount: 12.50 EUR\nMerchant: ACME",
    )
    await page.getByRole("button", { name: "Test" }).click()

    // Texto plano del conversor (la parte que revela que el HTML no parseó bien).
    await expect(page.getByText(/Subject: Receipt/).first()).toBeVisible()
    // Explicación literal devuelta por el backend.
    await expect(page.getByText(/anchored on the first match/)).toBeVisible()
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
})
