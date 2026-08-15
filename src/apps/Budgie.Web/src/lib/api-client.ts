import createClient from "openapi-fetch"
import type { paths } from "./api-types"
import i18n from "@/i18n/config"

const REFRESH_URL = "/api/users/refresh"

let isRefreshing = false
let refreshPromise: Promise<boolean> | null = null

async function refreshToken(): Promise<boolean> {
  const refreshToken = localStorage.getItem("refreshToken")
  if (!refreshToken) return false

  try {
    const response = await fetch(REFRESH_URL, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken }),
    })

    if (!response.ok) {
      localStorage.removeItem("token")
      localStorage.removeItem("refreshToken")
      return false
    }

    const data = await response.json()
    localStorage.setItem("token", data.token)
    localStorage.setItem("refreshToken", data.refreshToken)
    return true
  } catch {
    localStorage.removeItem("token")
    localStorage.removeItem("refreshToken")
    return false
  }
}

async function authFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const token = localStorage.getItem("token")

  // input may be a Request object (from openapi-fetch) — read its headers if init has none
  const sourceHeaders = init?.headers
    ? new Headers(init.headers)
    : input instanceof Request
      ? new Headers(input.headers)
      : new Headers()

  const headers = sourceHeaders
  if (token) {
    headers.set("Authorization", `Bearer ${token}`)
  }

  // Sync the UI language to the backend so localized errors are returned.
  const activeLanguage = (i18n.language ?? "es").split("-")[0].toLowerCase()
  headers.set("Accept-Language", ["es", "en"].includes(activeLanguage) ? activeLanguage : "es")

  const response = await fetch(input, { ...init, headers })

  if (response.status === 401 && !input.toString().includes(REFRESH_URL)) {
    if (!isRefreshing) {
      isRefreshing = true
      refreshPromise = refreshToken().finally(() => {
        isRefreshing = false
        refreshPromise = null
      })
    }

    const refreshed = await refreshPromise
    if (refreshed) {
      const newToken = localStorage.getItem("token")
      const retrySource = init?.headers
        ? new Headers(init.headers)
        : input instanceof Request
          ? new Headers(input.headers)
          : new Headers()
      const retryHeaders = retrySource
      if (newToken) {
        retryHeaders.set("Authorization", `Bearer ${newToken}`)
      }
      retryHeaders.set("Accept-Language", headers.get("Accept-Language") ?? "es")
      return fetch(input, { ...init, headers: retryHeaders })
    }

    window.location.href = "/login"
  }

  return response
}

const client = createClient<paths>({
  baseUrl: "",
  fetch: authFetch,
})

export default client
