import type { Page, Route } from "@playwright/test"

/**
 * MSW-style API mock helper built on Playwright's `page.route`.
 *
 * Register handlers keyed by OpenAPI method+path. Resolved top-to-bottom on the
 * first matching wildcard. Responses can be a static body (JSON) or a function
 * that inspects the request (e.g. to seed/assert payloads).
 *
 * Usage:
 *   await mockApi(page).handle("GET", "/api/accounts", { data: [...] })
 *   await mockApi(page)
 *     .handle("GET", "/api/auth/status", { initialized: true, allowRegistration: true })
 *     .handle("POST", "/api/auth/login", ({ request }) => {
 *       const { username } = request.postDataJSON() as { username: string }
 *       return { status: 200, body: { token: "x", username } }
 *     })
 */

export type MockRequest = {
  method: string
  url: string
  postDataJSON: () => unknown
  /**
   * Cabeceras de la petición. Existe para que un handler pueda COMPROBAR el Content-Type:
   * sin esto el harness perdona el defecto del slice 1, donde el PUT del umbral viajaba sin
   * `Content-Type` y fetch lo enviaba como `text/plain` — que la API real rechaza con 415 y
   * ningún test veía, porque el stub responds bien a cualquier cuerpo (§9.1 de la spec).
   */
  headers: () => Record<string, string>
}

export type HandlerFn = (ctx: {
  route: Route
  request: MockRequest
  url: URL
}) => { status?: number; body: unknown } | undefined

interface Handler {
  method: string
  pattern: RegExp
  respond: HandlerFn | unknown
}

const METHOD_WILDCARD = "*"

const mocksByPage = new WeakMap<Page, ApiMock>()

export class ApiMock {
  private handlers: Handler[] = []
  private installed = false

  constructor(private readonly page: Page) {}

  handle(method: string, pathMatcher: string, respond: HandlerFn | unknown): this {
    const pattern = this.toPattern(pathMatcher)
    // Nuevos handlers al frente para que un mock posterior pueda sobrescribir uno previo
    this.handlers.unshift({ method: method.toUpperCase(), pattern, respond })
    if (!this.installed) this.install()
    return this
  }

  private toPattern(path: string): RegExp {
    const escaped = path.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")
    return new RegExp(`^${escaped.replace(/\\\*|\\\{/g, (m) => (m === "\\*" ? ".*" : "\\{"))}$`)
  }

  private install(): void {
    this.installed = true
    this.page.route("**/api/**", (route) => {
      const url = new URL(route.request().url())
      // Solo interceptar rutas de API reales. El glob `**/api/**` también
      // matchearía módulos fuente que contengan "/api/" en su path
      // (p.ej. src/modules/import/api/importClient.ts) y los abortaría.
      if (!url.pathname.startsWith("/api/")) return route.fallback()
      return this.route(route)
    })
  }

  private async route(route: Route): Promise<void> {
    const request = route.request()
    const method = request.method()
    const url = new URL(request.url())
    const path = url.pathname

    const mockRequest: MockRequest = {
      method,
      url: request.url(),
      postDataJSON: () => {
        const post = request.postDataJSON()
        return post && typeof post === "object" ? post : undefined
      },
      headers: () => {
        const out: Record<string, string> = {}
        for (const [key, value] of Object.entries(request.headers())) out[key.toLowerCase()] = value
        return out
      },
    }

    for (const handler of this.handlers) {
      if (handler.method !== METHOD_WILDCARD && handler.method !== method) continue
      if (!handler.pattern.test(path)) continue

      const result =
        typeof handler.respond === "function"
          ? (handler.respond as HandlerFn)({ route, request: mockRequest, url })
          : { body: handler.respond }

      if (!result) return route.abort()
      const body = result.body ?? null
      await route.fulfill({
        status: result.status ?? 200,
        contentType: "application/json",
        body: body === null ? "null" : JSON.stringify(body),
      })
      return
    }

    // No handler matched: fail loudly so tests don't silently hit the real API.
    await route.abort("failed")
  }
}

export function mockApi(page: Page): ApiMock {
  let existing = mocksByPage.get(page)
  if (!existing) {
    existing = new ApiMock(page)
    mocksByPage.set(page, existing)
  }
  return existing
}
