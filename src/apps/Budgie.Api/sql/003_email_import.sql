-- Budgie email import: provider connections, parsing rules, staging inbox
-- Slice 1 — Schema DDL for the EmailImport module (spec §3.1, §3.2, §3.3)
--
-- Design notes that are not obvious from the columns:
--   * "SyncCursor" is an opaque string. Its meaning belongs to the provider
--     implementation (a Gmail historyId today), which is why this table has no
--     provider-specific columns. Adding a provider must not need a migration.
--   * UNIQUE("UserId","MessageId") on PendingEmailTransactions is the first of
--     two idempotency barriers; the second is the unique index on
--     Transactions."ExternalUid" (created in 002_import_runs.sql). See spec §3.5.

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

-- Note: "_SchemaVersions"."Version" has no UNIQUE constraint (see 001_initial_schema.sql), so
-- the "ON CONFLICT DO NOTHING" form used by the earlier scripts is a no-op and appends a new row
-- on every startup. Guarding with NOT EXISTS makes this insert genuinely idempotent without
-- altering the shared table.
INSERT INTO "_SchemaVersions" ("Version")
SELECT '003_email_import'
WHERE NOT EXISTS (SELECT 1 FROM "_SchemaVersions" WHERE "Version" = '003_email_import');
