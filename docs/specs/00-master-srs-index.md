# Budgie — Master Software Requirement Specification (SRS) & Architecture Blueprint

## Executive Summary

**Budgie** is a self-hosted, personal finance management platform designed for high performance, maximum privacy, zero-trust system initialization, and rock-solid ledger integrity. 

It is engineered as a **Modular Monolith** using **Clean Architecture**, with a **.NET 10 Native AOT** Web API backend and a **React Router v7 (SPA Mode) + TypeScript + Tailwind CSS v4 + Base UI / shadcn/ui** frontend.

---

## Specification Index

1. **[System Architecture & Native AOT Backend](file:///home/andleo/Proyectos/Budgie/docs/specs/01-system-architecture-aot.md)**
   - Modular Monolith layout & Clean Architecture layer boundaries.
   - Native AOT compatibility (`<PublishAot>true</PublishAot>`, `JsonSerializerContext`, Dapper source generation).
   - CQRS records with Mediator and ErrorOr.
   - **Event-Driven Command Handlers & Domain Events (Single Responsibility Principle via Mediator `INotification` & `INotificationHandler<T>`)**.
   - Observability with OpenTelemetry (OTLP Tracing, Metrics, Logging).
   - Internationalization (i18n) middleware with Spanish (`es`) fallback.

2. **[Security, Auth & Zero-Trust Onboarding](file:///home/andleo/Proyectos/Budgie/docs/specs/02-security-and-onboarding.md)**
   - First-run detection (`/api/auth/status` -> `{ initialized: false, allowRegistration: true }`).
   - First-user automatic `Admin` role assignment.
   - Registration policies (`ENABLE_PUBLIC_REGISTRATION=false` default).
   - Admin user management: Token-based invite links & direct administrator provisioning.
   - Auth security: 15-minute JWT access tokens & sliding HttpOnly / SameSite=Strict refresh token cookies.

3. **[Domain Ledger, Accounts & Split-Expense Engine](file:///home/andleo/Proyectos/Budgie/docs/specs/03-domain-ledger-and-split-engine.md)**
   - Account types: Bank Accounts (Checking/Savings), Credit Cards, Cash, Loans/Debts.
   - Strict Ledger Rule: `ControlStartDate` immutability/validation preventing backdated entries.
   - Credit Card billing cycles (`StatementClosingDay`, `PaymentDueDay`, immediate vs cycle expenses).
   - Multi-party split expense model: exact amount, equal fractions (1/N), percentages.
   - Virtual receivable/debt tracking under external Person profiles.
   - Settlement workflows: immediate vs deferred debt resolution.

4. **[Budgeting, Recurring Engine, Multi-Currency & Backups](file:///home/andleo/Proyectos/Budgie/docs/specs/04-budgeting-recurring-and-backups.md)**
   - Fixed monthly category budgets with real-time 80% warning and 100% exceeded indicators.
   - Recurring transactions engine with customizable frequencies and automated/template scheduling.
   - Multi-currency conversions, base currency settings, and per-transaction overrides.
   - Data exports (CSV/JSON) and portable full-account JSON snapshots.

5. **[Frontend Architecture, UX & OpenAPI Integration](file:///home/andleo/Proyectos/Budgie/docs/specs/05-frontend-architecture-and-i18n.md)**
   - Bun.js + Vite + React Router v7 + TanStack Query v5 + React Hook Form + Zod.
   - Base UI primitives + shadcn/ui components + Dark/Light mode theme system.
   - Client-side i18n (`react-i18next`) with full `es` and `en` translations and localized validation error mapping.
   - Auto-generated OpenAPI TypeScript client and hooks via `openapi-ts` + `openapi-react-query`.

6. **[Testing Strategy, ~100% Coverage & Git Conventions](file:///home/andleo/Proyectos/Budgie/docs/specs/06-testing-strategy-and-git-conventions.md)**
   - High-rigor testing strategy targeting ~100% code coverage across all layers.
   - Unit tests (xUnit, NSubstitute, FluentAssertions) for Aggregates, Value Objects, CQRS Handlers, Event Handlers, and bilingual i18n errors.
   - Integration tests (WebApplicationFactory + PostgreSQL) for end-to-end user flows, Zero-Trust Onboarding, Strict Ledger rules, Splits, and Snapshots.
   - Conventional Commits 1.0.0 standards and approved scopes (`feat`, `fix`, `test`, `refactor`, `perf`, `docs`, `chore`).
