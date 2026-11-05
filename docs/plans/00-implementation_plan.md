# Implementation Plan — Budgie Master Architecture & SRS Execution

This plan outlines the systematic implementation of the Software Requirement Specification (SRS) and Master Architecture for **Budgie**: a self-hosted Personal Finance Platform with a .NET 10 Native AOT backend and React Router v7 / Vite / Tailwind v4 / Base UI frontend.

## User Review Required

> [!NOTE]
> All architectural and functional decisions from the specification dialogues have been integrated:
> - Dual onboarding (Token Invite Links + Direct Admin User Provisioning).
> - Strict Ledger Rule (`ControlStartDate` immutable / validated, initial balance recorded on start date).
> - Event-Driven Command Handlers using `Mediator` (`INotification` / `INotificationHandler<T>`) for strict Single Responsibility Principle (SRP).
> - Clean Architecture layer isolation (moving all remaining SQL from Application handlers to Infrastructure repositories).
> - Category-level monthly budgets with 80% warning and 100% exceeded visual alerts.
> - High-rigor test suite targeting ~100% code coverage.
> - Conventional Commits 1.0.0 enforcement.

---

## Proposed Changes

### 1. Shared Abstractions & Event-Driven Foundation (`Budgie.Shared`)

- **Domain Value Objects & Types**:
  - Update `AccountType` enum to include `Cash` and `Loan`/`Debt` alongside `Checking`, `Savings`, `Credit`.
  - Add domain events / notifications base interfaces using `Mediator.INotification`.
  - Ensure bilingual error message catalogs (`es` default, `en` fallback) for all shared domain value objects.

#### [MODIFY] [SharedEnums.cs](src/core/Budgie.Shared/Enums/SharedEnums.cs)
#### [NEW] [DomainEvents.cs](src/core/Budgie.Shared/Domain/Events/DomainEvents.cs)

---

### 2. Users Module (`Budgie.Modules.Users`)

- **Zero-Trust Initialization & Admin Roles**:
  - Add `Role` field (`Admin = 1`, `User = 2`) to `User` entity and database schema.
  - Implement first-run detection in registration policy: first user automatically receives `Admin` role.
  - Implement token-based invitation generation (`UserInvite` entity, repository, and commands).
  - Implement direct administrator user provisioning command (`CreateUserByAdminCommand`).
  - Migrate JWT Refresh Token delivery to secure `HttpOnly`, `SameSite=Strict` cookies with sliding rotation.
  - Move all remaining raw SQL out of Application query handlers into `Infrastructure/Database/UserRepository.cs`.

#### [MODIFY] [User.cs](src/core/Budgie.Modules.Users/Domain/User.cs)
#### [NEW] [UserInvite.cs](src/core/Budgie.Modules.Users/Domain/UserInvite.cs)
#### [MODIFY] [RegistrationPolicy.cs](src/core/Budgie.Modules.Users/Infrastructure/RegistrationPolicy.cs)
#### [NEW] [CreateInviteCommandHandler.cs](src/core/Budgie.Modules.Users/Application/Invites/CreateInviteCommandHandler.cs)
#### [NEW] [AdminCreateUserCommandHandler.cs](src/core/Budgie.Modules.Users/Application/Admin/CreateUser/AdminCreateUserCommandHandler.cs)
#### [MODIFY] [Repositories.cs](src/core/Budgie.Modules.Users/Infrastructure/Database/Repositories.cs)

---

### 3. Finances Module & Strict Ledger Engine (`Budgie.Modules.Finances`)

- **Strict Ledger Rule (`ControlStartDate`)**:
  - Add `ControlStartDate` to `Account` entity and database schema.
  - Add invariant validation: reject any transaction with `Date < Account.ControlStartDate`.
  - Update account creation to record initial balance transaction on `ControlStartDate`.
- **Event-Driven Command Handlers**:
  - Refactor `CreateTransactionCommandHandler` to purely create and persist the transaction aggregate, then publish `TransactionCreatedEvent`.
  - Implement `UpdateAccountBalanceOnTransactionCreated` (`INotificationHandler<TransactionCreatedEvent>`).
  - Implement `RecordSplitDebtsOnTransactionCreated` (`INotificationHandler<TransactionCreatedEvent>`).
  - Implement `CheckBudgetLimitOnTransactionCreated` (`INotificationHandler<TransactionCreatedEvent>`).
- **Multi-Party Split Allocation & Flexible Settlement**:
  - Implement split calculation modes (Exact amount, Equal 1/N, Percentage).
  - Implement flexible "Settle Debt" handler supporting specific split settlement and lump-sum repayments with destination account selection.
- **Budgeting & Monthly Spending Limits**:
  - Add `MonthlyBudget` to `Category` entity and database schema.
  - Implement `GetCategoryBudgetsQuery` returning spent amount, remaining budget, percentage, and threshold status (`Normal`, `Warning`, `Exceeded`).
- **Export & Full JSON Snapshot**:
  - Implement transaction CSV/JSON export query handlers.
  - Implement full user account snapshot backup (`ExportSnapshotQuery`) and restore (`RestoreSnapshotCommand`).
- **Clean Architecture Refactoring**:
  - Move raw SQL queries from Goals, Categories, Reports, and Debts application handlers into Dapper repository implementations in `Infrastructure/Database/`.

#### [MODIFY] [Account.cs](src/core/Budgie.Modules.Finances/Domain/Accounts/Account.cs)
#### [MODIFY] [Category.cs](src/core/Budgie.Modules.Finances/Domain/Categories/Category.cs)
#### [MODIFY] [CreateTransactionCommandHandler.cs](src/core/Budgie.Modules.Finances/Application/Transactions/CreateTransaction/CreateTransactionCommandHandler.cs)
#### [NEW] [FinanceEvents.cs](src/core/Budgie.Modules.Finances/Domain/Events/FinanceEvents.cs)
#### [NEW] [UpdateAccountBalanceHandler.cs](src/core/Budgie.Modules.Finances/Application/Transactions/EventHandlers/UpdateAccountBalanceHandler.cs)
#### [NEW] [RecordSplitDebtsHandler.cs](src/core/Budgie.Modules.Finances/Application/Transactions/EventHandlers/RecordSplitDebtsHandler.cs)
#### [NEW] [SettleDebtCommandHandler.cs](src/core/Budgie.Modules.Finances/Application/Debts/SettleDebt/SettleDebtCommandHandler.cs)
#### [NEW] [ExportSnapshotQueryHandler.cs](src/core/Budgie.Modules.Finances/Application/Export/ExportSnapshotQueryHandler.cs)
#### [NEW] [RestoreSnapshotCommandHandler.cs](src/core/Budgie.Modules.Finances/Application/Export/RestoreSnapshotCommandHandler.cs)
#### [MODIFY] [Repositories.cs](src/core/Budgie.Modules.Finances/Infrastructure/Database/Repositories.cs)

---

### 4. Relationships Module (`Budgie.Modules.Relationships`)

- **Clean Architecture & Repository Separation**:
  - Move direct `IDbConnectionFactory` queries from Person and Tag handlers into `Infrastructure/Database/PersonRepository.cs` and `TagRepository.cs`.

#### [MODIFY] [Repositories.cs](src/core/Budgie.Modules.Relationships/Infrastructure/Database/Repositories.cs)

---

### 5. API Host, Native AOT & Localization Middleware (`Budgie.Api`)

- **Request Culture Middleware**:
  - Add `RequestCultureMiddleware` inspecting `Accept-Language` (Spanish `es` fallback, English `en`).
- **Native AOT Type Registration**:
  - Register all new command, query, DTO, and event payload types in `BudgieJsonContext`.
- **Database Schema Updates**:
  - Add SQL script `002_master_architecture_updates.sql` adding `Role` on `Users`, `ControlStartDate` on `Accounts`, `MonthlyBudget` on `Categories`, and `UserInvites` table.

#### [NEW] [002_master_architecture_updates.sql](src/apps/Budgie.Api/sql/002_master_architecture_updates.sql)
#### [MODIFY] [BudgieJsonContext.cs](src/apps/Budgie.Api/BudgieJsonContext.cs)
#### [MODIFY] [Program.cs](src/apps/Budgie.Api/Program.cs)

---

### 6. Frontend SPA Alignment (`src/apps/Budgie.Web`)

- **OpenAPI Synchronization**:
  - Regenerate `src/lib/api-types.ts` from updated OpenAPI schema.
- **Client Fetcher Interceptor**:
  - Update `api-client.ts` to automatically send `Accept-Language: es` (or active language) and manage HttpOnly cookie refresh.
- **Features & UI Components**:
  - Account Creation modal with `ControlStartDate` and Account Type selector (`Bank`, `CreditCard`, `Cash`, `Loan`).
  - Transaction Split modal with Exact Amount, 1/N, and % calculation modes, and Deferred vs Immediate settlement toggles.
  - Settle Debt action drawer in Person profile and Debts views.
  - Budget progress bars with 80% warning and 100% exceeded alert styles.
  - Snapshot export/import in Settings.
  - Full translations updated in `src/i18n/locales/es.json` and `en.json`.

#### [MODIFY] [api-client.ts](src/apps/Budgie.Web/src/lib/api-client.ts)
#### [MODIFY] [es.json](src/apps/Budgie.Web/src/i18n/locales/es.json)
#### [MODIFY] [en.json](src/apps/Budgie.Web/src/i18n/locales/en.json)

---

### 7. High-Rigor Test Suite (~100% Code Coverage)

- **Unit Tests (`Budgie.*.UnitTests`)**:
  - Test all Command Handlers, Query Handlers, Domain Value Objects, and `INotificationHandler` event consumers.
  - Validate bilingual error generation (`es` and `en`).
- **Integration Tests (`Budgie.IntegrationTests`)**:
  - Test Zero-Trust onboarding flow.
  - Test `ControlStartDate` rejection constraint.
  - Test multi-party split expense creation and subsequent debt settlement.
  - Test budget threshold calculations.
  - Test snapshot export and restore.

#### [NEW] [LedgerControlStartDateTests.cs](tests/Budgie.Modules.Finances.UnitTests/LedgerControlStartDateTests.cs)
#### [NEW] [EventDrivenSplitSettlementTests.cs](tests/Budgie.Modules.Finances.UnitTests/EventDrivenSplitSettlementTests.cs)
#### [NEW] [ZeroTrustOnboardingIntegrationTests.cs](tests/Budgie.IntegrationTests/ZeroTrustOnboardingIntegrationTests.cs)
#### [NEW] [SnapshotBackupRestoreIntegrationTests.cs](tests/Budgie.IntegrationTests/SnapshotBackupRestoreIntegrationTests.cs)

---

## Verification Plan

### Automated Tests
- Run complete test suite:
  ```bash
  dotnet test --verbosity normal
  ```
- Run test coverage collector:
  ```bash
  dotnet test --collect:"XPlat Code Coverage"
  ```
- Run frontend typecheck and build:
  ```bash
  cd src/apps/Budgie.Web && npm run build
  ```

### Native AOT Build Verification
- Verify backend compiles in Release mode:
  ```bash
  dotnet build -c Release
  ```
