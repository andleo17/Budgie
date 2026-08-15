# 05 — Frontend Architecture, UX & OpenAPI Integration

## 1. Frontend Runtime & Technology Stack

Budgie's web interface is a modern Single Page Application (SPA) designed for rapid interaction, full accessibility, and strict type safety.

- **Runtime & Package Manager**: [Bun.js](https://bun.sh)
- **Build Tool**: [Vite](https://vitejs.dev) + TypeScript (Strict Mode)
- **Routing**: [React Router v7](https://reactrouter.com) (SPA Mode)
- **Data Fetching & Cache**: [TanStack Query v5](https://tanstack.com/query)
- **Form Management**: [React Hook Form](https://react-hook-form.com) + [Zod v4](https://zod.dev)
- **Styling**: [Tailwind CSS v4](https://tailwindcss.com)
- **Component Primitives**: [Base UI](https://base-ui.com) (`@base-ui/react`) + [shadcn/ui](https://ui.shadcn.com)
- **Internationalization**: [i18next](https://www.i18next.com) + [react-i18next](https://react.i18next.com)

---

## 2. Directory Structure

```
src/apps/Budgie.Web/
├── package.json
├── vite.config.ts
├── src/
│   ├── components/                # Reusable UI primitives (Button, Dialog, Drawer, Card, etc.)
│   │   ├── ui/                    # Base UI + shadcn/ui components
│   │   ├── layout/                # Main layout, Sidebar, Topbar, Mobile Navigation
│   │   └── common/                # Theme toggle, Language switcher, Currency display
│   ├── modules/                   # Feature modules
│   │   ├── auth/                  # Login, Register, Invite claim, Setup wizard
│   │   ├── dashboard/             # Widgets, Net worth, Cash flow, Category breakdown charts
│   │   ├── accounts/              # Account list, Account modal (ControlStartDate, CC settings)
│   │   ├── transactions/          # Transaction ledger, Split modal, Period filter
│   │   ├── categories/            # Category hierarchy tree, Monthly budget thresholds
│   │   ├── persons/               # People directory, Debts & Receivables, Settle modal, Tags
│   │   ├── goals/                 # Financial targets & progress
│   │   ├── settings/              # Base currency, language, appearance, backup/export
│   │   └── profile/               # User account profile
│   ├── i18n/                      # Localization engine
│   │   ├── config.ts              # i18next init & Zod error map
│   │   ├── locales/
│   │   │   ├── es.json            # Spanish translations (Default)
│   │   │   └── en.json            # English translations
│   │   ├── LanguageProvider.tsx   # React context provider
│   │   └── useLanguage.ts         # Hook for locale switching
│   └── lib/                       # Utilities and API clients
│       ├── api-client.ts          # openapi-fetch wrapper with JWT auto-refresh & Accept-Language
│       ├── api-types.ts           # Auto-generated TypeScript types from OpenAPI spec
│       ├── format.ts              # Localized currency & date formatters
│       └── utils.ts               # Class merge helpers
```

---

## 3. Automated Type-Safe API Client (`openapi-ts` + `openapi-react-query`)

### Code Generation Workflow
1. The backend exposes the OpenAPI 3.1 schema at `/openapi/v1.json`.
2. Frontend CLI script `npm run api:gen` executes `openapi-typescript` to regenerate `src/lib/api-types.ts`.
3. Client uses `openapi-fetch` and `openapi-react-query` to provide fully typed mutation and query hooks without manual API interface boilerplate.

### Authentication & Language Interceptors (`api-client.ts`)
The `api-client.ts` fetch wrapper automatically:
- Attaches `Authorization: Bearer <token>` when available.
- Synchronizes the current UI language to the `Accept-Language` header (`es` or `en`).
- Handles `401 Unauthorized` by invoking the sliding `/api/users/refresh` rotation seamlessly.

---

## 4. Internationalization (i18n) & Localized Form Validation

1. **Dual Language Support**:
   - Complete translations for **Spanish (`es`)** and **English (`en`)**.
   - Global Language Selector in the top bar / mobile drawer toggles languages instantly without page reload.

2. **Zod Localized Validation Errors**:
   - `z.setErrorMap` hooks into `i18n.t()` so all form validation messages (required fields, min/max lengths, email patterns, positive numbers) display in the active user language.

3. **Locale-Aware Formatting**:
   - Currency formatting respects currency code and active locale (e.g. `$ 1.250,00` vs `$1,250.00`).
   - Dates formatted via `date-fns` using the corresponding locale (`es` / `enUS`).

---

## 5. Accessibility (a11y) & UX Standards

- **Keyboard Navigation**: Full tab index, enter/escape trapping for modals, drawers, and combobox dropdowns powered by Base UI primitives.
- **Screen Reader Support**: Comprehensive `aria-label`, `aria-expanded`, `aria-describedby`, and role annotations.
- **Theme Provider**: Dark, Light, and System preference options with smooth transitions and zero layout shifts.
- **Feedback & Feedback Skeletons**: Sonner toast notifications for actions, subtle micro-animations, and loading skeleton states on initial data fetch.
