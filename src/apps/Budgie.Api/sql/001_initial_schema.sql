-- Budgie initial schema
-- Replaces EF Core migrations. Run at startup if UseInMemoryDatabase != true.

CREATE TABLE IF NOT EXISTS "_SchemaVersions" (
    "Id" SERIAL PRIMARY KEY,
    "Version" VARCHAR(50) NOT NULL,
    "AppliedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);

-- Users module
CREATE TABLE IF NOT EXISTS "Users" (
    "Id" UUID PRIMARY KEY,
    "Email" VARCHAR(255) NOT NULL,
    "Name" VARCHAR(100) NOT NULL,
    "PasswordHash" VARCHAR(255) NOT NULL,
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    "UpdatedAt" TIMESTAMP WITHOUT TIME ZONE
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Email" ON "Users"("Email");

CREATE TABLE IF NOT EXISTS "UserSettings" (
    "UserId" UUID PRIMARY KEY REFERENCES "Users"("Id") ON DELETE CASCADE,
    "MainCurrency" VARCHAR(3) NOT NULL,
    "DefaultCreditConsumptionMode" INT NOT NULL DEFAULT 0,
    "Language" VARCHAR(10) NOT NULL DEFAULT 'en',
    "IsConfigured" BOOLEAN NOT NULL DEFAULT FALSE,
    "SupportedCurrencies" JSONB NOT NULL DEFAULT '[]',
    "TransactionPageSize" INT NOT NULL DEFAULT 100,
    "DefaultRecurringApplicability" INT NOT NULL DEFAULT 0,
    "AutoApplyOnLoad" BOOLEAN NOT NULL DEFAULT TRUE
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserSettings_UserId" ON "UserSettings"("UserId");

CREATE TABLE IF NOT EXISTS "RefreshTokens" (
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "TokenHash" VARCHAR(88) NOT NULL,
    "ExpiresAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    "IsRevoked" BOOLEAN NOT NULL DEFAULT FALSE
);
CREATE INDEX IF NOT EXISTS "IX_RefreshTokens_TokenHash" ON "RefreshTokens"("TokenHash");

-- Relationships module
CREATE TABLE IF NOT EXISTS "Persons" (
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Name" VARCHAR(100) NOT NULL,
    "PhotoPath" VARCHAR(500),
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_Persons_UserId" ON "Persons"("UserId");

CREATE TABLE IF NOT EXISTS "Tags" (
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Name" VARCHAR(50) NOT NULL,
    "Color" VARCHAR(7),
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_Tags_UserId" ON "Tags"("UserId");

CREATE TABLE IF NOT EXISTS "PersonTags" (
    "PersonId" UUID NOT NULL REFERENCES "Persons"("Id") ON DELETE CASCADE,
    "TagId" UUID NOT NULL REFERENCES "Tags"("Id") ON DELETE CASCADE,
    PRIMARY KEY ("PersonId", "TagId")
);

-- Finances module: Accounts
CREATE TABLE IF NOT EXISTS "Accounts" (
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Name" VARCHAR(100) NOT NULL,
    "Balance" DECIMAL(18,2) NOT NULL,
    "Currency" VARCHAR(3) NOT NULL,
    "Type" INT NOT NULL,
    "CreditLimit" DECIMAL(18,2),
    "CreditLimitCurrency" VARCHAR(3),
    "CreditConsumptionMode" INT NOT NULL DEFAULT 0,
    "AnnualInterestRate" DECIMAL(18,4),
    "BillingCycleDayInclusive" BOOLEAN NOT NULL DEFAULT TRUE,
    "BillingCycleDay" INT,
    "PaymentDueDay" INT,
    "InterestCalculationMethod" INT NOT NULL DEFAULT 0,
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL
);
CREATE INDEX IF NOT EXISTS "IX_Accounts_UserId" ON "Accounts"("UserId");

-- Finances module: Categories
CREATE TABLE IF NOT EXISTS "Categories" (
    "Id" UUID PRIMARY KEY,
    "UserId" UUID,
    "Name" VARCHAR(50) NOT NULL,
    "Color" VARCHAR(7),
    "Icon" VARCHAR(50),
    "Type" INT NOT NULL,
    "IsSystem" BOOLEAN NOT NULL DEFAULT FALSE,
    "ParentId" UUID REFERENCES "Categories"("Id") ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS "IX_Categories_UserId" ON "Categories"("UserId");

-- Finances module: Transactions
CREATE TABLE IF NOT EXISTS "Transactions" (
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "AccountId" UUID NOT NULL REFERENCES "Accounts"("Id") ON DELETE CASCADE,
    "CategoryId" UUID REFERENCES "Categories"("Id") ON DELETE SET NULL,
    "Amount" DECIMAL(18,2) NOT NULL,
    "Currency" VARCHAR(3) NOT NULL,
    "ExchangeRate" DECIMAL(18,4) NOT NULL DEFAULT 1,
    "Date" TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    "Notes" VARCHAR(500),
    "Type" INT NOT NULL,
    "Installments" INT NOT NULL DEFAULT 1,
    "LinkedTransactionId" UUID,
    "ParentTransactionId" UUID REFERENCES "Transactions"("Id") ON DELETE SET NULL,
    "IsInitialBalance" BOOLEAN NOT NULL DEFAULT FALSE,
    "InstallmentGroupId" UUID,
    "InstallmentNumber" INT NOT NULL DEFAULT 1,
    "TotalInstallments" INT NOT NULL DEFAULT 1,
    "InterestRate" DECIMAL(18,4) NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS "IX_Transactions_UserId" ON "Transactions"("UserId");
CREATE INDEX IF NOT EXISTS "IX_Transactions_AccountId" ON "Transactions"("AccountId");
CREATE INDEX IF NOT EXISTS "IX_Transactions_ParentTransactionId" ON "Transactions"("ParentTransactionId");
CREATE INDEX IF NOT EXISTS "IX_Transactions_Date" ON "Transactions"("Date");

-- Finances module: TransactionParticipants
CREATE TABLE IF NOT EXISTS "TransactionParticipants" (
    "Id" UUID PRIMARY KEY,
    "TransactionId" UUID NOT NULL REFERENCES "Transactions"("Id") ON DELETE CASCADE,
    "PersonId" UUID NOT NULL REFERENCES "Persons"("Id") ON DELETE CASCADE,
    "ContributedAmount" DECIMAL(18,2) NOT NULL,
    "ContributedCurrency" VARCHAR(3) NOT NULL,
    "RemainingAmount" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "RemainingCurrency" VARCHAR(3) NOT NULL,
    "IsTrackingOnly" BOOLEAN NOT NULL DEFAULT FALSE,
    "DestinationAccountId" UUID REFERENCES "Accounts"("Id") ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS "IX_TransactionParticipants_TransactionId" ON "TransactionParticipants"("TransactionId");

-- Finances module: RepaymentDetails (replaces RepaidParticipants JSON)
CREATE TABLE IF NOT EXISTS "RepaymentDetails" (
    "RepaymentTransactionId" UUID NOT NULL REFERENCES "Transactions"("Id") ON DELETE CASCADE,
    "RepaidParticipantId" UUID NOT NULL REFERENCES "TransactionParticipants"("Id") ON DELETE CASCADE,
    "Amount" DECIMAL(18,2) NOT NULL,
    PRIMARY KEY ("RepaymentTransactionId", "RepaidParticipantId")
);

-- Finances module: ExchangeRates
CREATE TABLE IF NOT EXISTS "ExchangeRates" (
    "Id" UUID PRIMARY KEY,
    "FromCurrency" VARCHAR(3) NOT NULL,
    "ToCurrency" VARCHAR(3) NOT NULL,
    "Rate" DECIMAL(18,4) NOT NULL,
    "Date" TIMESTAMP WITHOUT TIME ZONE NOT NULL
);

-- Finances module: Goals
CREATE TABLE IF NOT EXISTS "Goals" (
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "AccountId" UUID NOT NULL REFERENCES "Accounts"("Id") ON DELETE CASCADE,
    "TargetAmount" DECIMAL(18,2) NOT NULL,
    "TargetCurrency" VARCHAR(3) NOT NULL,
    "TargetDate" TIMESTAMP WITHOUT TIME ZONE,
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_Goals_UserId" ON "Goals"("UserId");

-- Finances module: RecurringTransactions
CREATE TABLE IF NOT EXISTS "RecurringTransactions" (
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "AccountId" UUID NOT NULL REFERENCES "Accounts"("Id") ON DELETE CASCADE,
    "CategoryId" UUID REFERENCES "Categories"("Id") ON DELETE SET NULL,
    "Name" VARCHAR(100) NOT NULL,
    "Amount" DECIMAL(18,2) NOT NULL,
    "Currency" VARCHAR(3) NOT NULL,
    "ExchangeRate" DECIMAL(18,4) NOT NULL DEFAULT 1,
    "Notes" VARCHAR(500),
    "Type" INT NOT NULL,
    "Installments" INT NOT NULL DEFAULT 1,
    "IsInterestFree" BOOLEAN NOT NULL DEFAULT TRUE,
    "Frequency" INT NOT NULL DEFAULT 3,
    "CustomIntervalDays" INT,
    "StartDate" TIMESTAMP WITHOUT TIME ZONE,
    "EndDate" TIMESTAMP WITHOUT TIME ZONE,
    "EndAfterCount" INT,
    "EndType" INT,
    "Applicability" INT NOT NULL DEFAULT 0,
    "NextDueDate" TIMESTAMP WITHOUT TIME ZONE,
    "LastAppliedDate" TIMESTAMP WITHOUT TIME ZONE,
    "TimesApplied" INT NOT NULL DEFAULT 0,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE
);
CREATE INDEX IF NOT EXISTS "IX_RecurringTransactions_UserId" ON "RecurringTransactions"("UserId");

CREATE TABLE IF NOT EXISTS "RecurringTransactionParticipants" (
    "Id" UUID PRIMARY KEY,
    "RecurringTransactionId" UUID NOT NULL REFERENCES "RecurringTransactions"("Id") ON DELETE CASCADE,
    "PersonId" UUID NOT NULL REFERENCES "Persons"("Id") ON DELETE CASCADE,
    "ContributedAmount" DECIMAL(18,2) NOT NULL,
    "ContributedCurrency" VARCHAR(3) NOT NULL,
    "RemainingAmount" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "RemainingCurrency" VARCHAR(3) NOT NULL,
    "IsTrackingOnly" BOOLEAN NOT NULL DEFAULT FALSE,
    "DestinationAccountId" UUID REFERENCES "Accounts"("Id") ON DELETE SET NULL
);

-- Grant default permissions
INSERT INTO "_SchemaVersions" ("Version") VALUES ('001_initial') ON CONFLICT DO NOTHING;
