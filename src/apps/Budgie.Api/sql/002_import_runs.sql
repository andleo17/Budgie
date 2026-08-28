-- Budgie import runs + ExternalUid idempotency (Money Manager .mmbak)
-- Slice 1 — Schema DDL: ImportRuns table and ExternalUid columns for idempotent imports

CREATE TABLE IF NOT EXISTS "ImportRuns" (
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "FileHash" VARCHAR(64) NOT NULL,
    "FileName" VARCHAR(255),
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    "Counts" JSONB,
    UNIQUE("UserId", "FileHash")
);

CREATE INDEX IF NOT EXISTS "IX_ImportRuns_UserId" ON "ImportRuns"("UserId");

ALTER TABLE "Accounts" ADD COLUMN IF NOT EXISTS "ExternalUid" VARCHAR(100);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Accounts_UserId_ExternalUid" ON "Accounts"("UserId", "ExternalUid") WHERE "ExternalUid" IS NOT NULL;

ALTER TABLE "Categories" ADD COLUMN IF NOT EXISTS "ExternalUid" VARCHAR(100);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Categories_UserId_ExternalUid" ON "Categories"("UserId", "ExternalUid") WHERE "ExternalUid" IS NOT NULL;

ALTER TABLE "Transactions" ADD COLUMN IF NOT EXISTS "ExternalUid" VARCHAR(100);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Transactions_UserId_ExternalUid" ON "Transactions"("UserId", "ExternalUid") WHERE "ExternalUid" IS NOT NULL;

ALTER TABLE "RecurringTransactions" ADD COLUMN IF NOT EXISTS "ExternalUid" VARCHAR(100);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_RecurringTransactions_UserId_ExternalUid" ON "RecurringTransactions"("UserId", "ExternalUid") WHERE "ExternalUid" IS NOT NULL;

INSERT INTO "_SchemaVersions" ("Version") VALUES ('002_import_runs') ON CONFLICT DO NOTHING;
