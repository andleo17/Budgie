using System.Text.Json.Serialization;
using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Application.Connections;
using Budgie.Modules.EmailImport.Application.Connections.GetConnectionStatus;
using Budgie.Modules.EmailImport.Application.Inbox;
using Budgie.Modules.EmailImport.Application.Inbox.ApproveBatch;
using Budgie.Modules.EmailImport.Application.Inbox.GetPending;
using Budgie.Modules.EmailImport.Application.Rules;
using Budgie.Modules.EmailImport.Application.Rules.PreviewRule;
using Budgie.Modules.EmailImport.Presentation.Connections;
using Budgie.Modules.EmailImport.Presentation.Inbox;
using Budgie.Modules.EmailImport.Presentation.Rules;
using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Accounts;
using Budgie.Modules.Finances.Application.Accounts.CreateAccount;
using Budgie.Modules.Finances.Application.Accounts.GetAccountById;
using Budgie.Modules.Finances.Application.Accounts.GetAccounts;
using Budgie.Modules.Finances.Application.Accounts.UpdateAccount;
using Budgie.Modules.Finances.Application.Categories.CreateCategory;
using Budgie.Modules.Finances.Application.Categories.DeleteCategory;
using Budgie.Modules.Finances.Application.Categories.GetCategories;
using Budgie.Modules.Finances.Application.Categories.UpdateCategory;
using Budgie.Modules.Finances.Application.Debts.GetPersonDebts;
using Budgie.Modules.Finances.Application.Debts.SettleDebt;
using Budgie.Modules.Finances.Application.Export;
using Budgie.Modules.Finances.Application.Goals;
using Budgie.Modules.Finances.Application.Goals.CreateGoal;
using Budgie.Modules.Finances.Application.Goals.DeleteGoal;
using Budgie.Modules.Finances.Application.Goals.GetGoals;
using Budgie.Modules.Finances.Application.Goals.UpdateGoal;
using Budgie.Modules.Finances.Application.Import.CommitMoneyManagerImport;
using Budgie.Modules.Finances.Application.Import.PreviewMoneyManagerImport;
using Budgie.Modules.Finances.Application.Persons.GetPersonSummary;
using Budgie.Modules.Finances.Application.RecurringTransactions.ApplyAllDueRecurringTransactions;
using Budgie.Modules.Finances.Application.RecurringTransactions.ApplyRecurringTransaction;
using Budgie.Modules.Finances.Application.RecurringTransactions.CreateRecurringTransaction;
using Budgie.Modules.Finances.Application.RecurringTransactions.DeleteRecurringTransaction;
using Budgie.Modules.Finances.Application.RecurringTransactions.GetDueRecurringTransactions;
using Budgie.Modules.Finances.Application.RecurringTransactions.GetRecurringTransaction;
using Budgie.Modules.Finances.Application.RecurringTransactions.GetRecurringTransactions;
using Budgie.Modules.Finances.Application.RecurringTransactions.Shared;
using Budgie.Modules.Finances.Application.RecurringTransactions.UpdateRecurringTransaction;
using Budgie.Modules.Finances.Application.Reports.GetCategoryBudgets;
using Budgie.Modules.Finances.Application.Reports.GetCategorySpending;
using Budgie.Modules.Finances.Application.Reports.GetDashboardSummary;
using Budgie.Modules.Finances.Application.Reports.GetGoalsStatus;
using Budgie.Modules.Finances.Application.Reports.GetTransactionsSummary;
using Budgie.Modules.Finances.Application.Transactions;
using Budgie.Modules.Finances.Application.Transactions.BulkRepay;
using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Modules.Finances.Application.Transactions.DeleteTransaction;
using Budgie.Modules.Finances.Application.Transactions.GetTransactions;
using Budgie.Modules.Finances.Application.Transactions.Transfer;
using Budgie.Modules.Finances.Application.Transactions.UpdateTransaction;
using Budgie.Modules.Finances.Presentation.Accounts;
using Budgie.Modules.Finances.Presentation.Categories;
using Budgie.Modules.Finances.Presentation.Goals;
using Budgie.Modules.Finances.Presentation.Transactions;
using Budgie.Modules.Relationships.Application.Persons;
using Budgie.Modules.Relationships.Application.Persons.CreatePerson;
using Budgie.Modules.Relationships.Application.Persons.DeletePerson;
using Budgie.Modules.Relationships.Application.Persons.GetPersons;
using Budgie.Modules.Relationships.Application.Persons.UpdatePerson;
using Budgie.Modules.Relationships.Application.Tags;
using Budgie.Modules.Users.Application.Admin.CreateUser;
using Budgie.Modules.Users.Application.Invites;
using Budgie.Modules.Users.Application.Login;
using Budgie.Modules.Users.Application.Profile;
using Budgie.Modules.Users.Application.Profile.GetProfile;
using Budgie.Modules.Users.Application.Profile.UpdateProfile;
using Budgie.Modules.Users.Application.Refresh;
using Budgie.Modules.Users.Application.Register;
using Budgie.Modules.Users.Application.Settings;
using Budgie.Modules.Users.Application.Settings.UpdateSettings;

namespace Budgie.Api;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RegisterUserCommand))]
[JsonSerializable(typeof(Budgie.Modules.Users.Application.Register.RegistrationStatusResponse))]
[JsonSerializable(typeof(CreateInviteCommand))]
[JsonSerializable(typeof(CreateInviteResponse))]
[JsonSerializable(typeof(AdminCreateUserCommand))]
[JsonSerializable(typeof(LoginCommand))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(RefreshCommand))]
[JsonSerializable(typeof(UpdateSettingsCommand))]
[JsonSerializable(typeof(GetProfileQuery))]
[JsonSerializable(typeof(ProfileResponse))]
[JsonSerializable(typeof(UpdateProfileCommand))]
[JsonSerializable(typeof(UserSettingsResponse))]
[JsonSerializable(typeof(CreateAccountCommand))]
[JsonSerializable(typeof(CreateAccountRequest))]
[JsonSerializable(typeof(GetAccountsQuery))]
[JsonSerializable(typeof(List<AccountResponse>))]
[JsonSerializable(typeof(GetAccountByIdQuery))]
[JsonSerializable(typeof(AccountResponse))]
[JsonSerializable(typeof(UpdateAccountCommand))]
[JsonSerializable(typeof(UpdateAccountRequest))]
[JsonSerializable(typeof(CreateTransactionCommand))]
[JsonSerializable(typeof(TransactionParticipantDto))]
[JsonSerializable(typeof(List<TransactionParticipantDto>))]
[JsonSerializable(typeof(UpdateTransactionCommand))]
[JsonSerializable(typeof(UpdateTransactionRequest))]
[JsonSerializable(typeof(TransferCommand))]
[JsonSerializable(typeof(BulkRepayCommand))]
[JsonSerializable(typeof(DebtRepayment))]
[JsonSerializable(typeof(List<DebtRepayment>))]
[JsonSerializable(typeof(DeleteTransactionCommand))]
[JsonSerializable(typeof(GetTransactionsQuery))]
[JsonSerializable(typeof(PagedTransactionResponse))]
[JsonSerializable(typeof(TransactionResponse))]
[JsonSerializable(typeof(List<TransactionResponse>))]
[JsonSerializable(typeof(GetPersonDebtsQuery))]
[JsonSerializable(typeof(List<DebtResponse>))]
[JsonSerializable(typeof(DebtResponse))]
[JsonSerializable(typeof(GetPersonSummaryQuery))]
[JsonSerializable(typeof(PersonSummaryResponse))]
[JsonSerializable(typeof(SettleDebtCommand))]
[JsonSerializable(typeof(GetCategoriesQuery))]
[JsonSerializable(typeof(List<CategoryResponse>))]
[JsonSerializable(typeof(CategoryResponse))]
[JsonSerializable(typeof(CreateCategoryCommand))]
[JsonSerializable(typeof(UpdateCategoryCommand))]
[JsonSerializable(typeof(UpdateCategoryRequest))]
[JsonSerializable(typeof(DeleteCategoryCommand))]
[JsonSerializable(typeof(CreateGoalCommand))]
[JsonSerializable(typeof(UpdateGoalCommand))]
[JsonSerializable(typeof(UpdateGoalRequest))]
[JsonSerializable(typeof(DeleteGoalCommand))]
[JsonSerializable(typeof(GetGoalsQuery))]
[JsonSerializable(typeof(List<GoalResponse>))]
[JsonSerializable(typeof(GoalResponse))]
[JsonSerializable(typeof(CreateRecurringTransactionCommand))]
[JsonSerializable(typeof(UpdateRecurringTransactionCommand))]
[JsonSerializable(typeof(DeleteRecurringTransactionCommand))]
[JsonSerializable(typeof(ApplyRecurringTransactionCommand))]
[JsonSerializable(typeof(ApplyAllDueRecurringTransactionsCommand))]
[JsonSerializable(typeof(GetRecurringTransactionQuery))]
[JsonSerializable(typeof(GetRecurringTransactionsQuery))]
[JsonSerializable(typeof(List<RecurringTransactionResponse>))]
[JsonSerializable(typeof(RecurringTransactionResponse))]
[JsonSerializable(typeof(RecurringTransactionParticipantDto))]
[JsonSerializable(typeof(List<RecurringTransactionParticipantDto>))]
[JsonSerializable(typeof(GetDueRecurringTransactionsQuery))]
[JsonSerializable(typeof(List<DueRecurringResponse>))]
[JsonSerializable(typeof(DueRecurringResponse))]
[JsonSerializable(typeof(GetDashboardSummaryQuery))]
[JsonSerializable(typeof(DashboardSummary))]
[JsonSerializable(typeof(GetCategorySpendingQuery))]
[JsonSerializable(typeof(List<CategorySpending>))]
[JsonSerializable(typeof(CategorySpending))]
[JsonSerializable(typeof(GetCategoryBudgetsQuery))]
[JsonSerializable(typeof(List<CategoryBudget>))]
[JsonSerializable(typeof(CategoryBudget))]
[JsonSerializable(typeof(GetTransactionsSummaryQuery))]
[JsonSerializable(typeof(GetTransactionsSummaryResponse))]
[JsonSerializable(typeof(List<CurrencySummary>))]
[JsonSerializable(typeof(CurrencySummary))]
[JsonSerializable(typeof(GetGoalsStatusQuery))]
[JsonSerializable(typeof(List<GoalStatus>))]
[JsonSerializable(typeof(GoalStatus))]
[JsonSerializable(typeof(ExportTransactionsQuery))]
[JsonSerializable(typeof(PreviewMoneyManagerImportCommand))]
[JsonSerializable(typeof(CommitMoneyManagerImportCommand))]
[JsonSerializable(typeof(PreviewMoneyManagerImportResponse))]
[JsonSerializable(typeof(List<PreviewCurrency>))]
[JsonSerializable(typeof(PreviewCurrency))]
[JsonSerializable(typeof(List<PreviewAccount>))]
[JsonSerializable(typeof(PreviewAccount))]
[JsonSerializable(typeof(List<PreviewCategory>))]
[JsonSerializable(typeof(PreviewCategory))]
[JsonSerializable(typeof(PreviewTransactionsSummary))]
[JsonSerializable(typeof(List<PreviewBudget>))]
[JsonSerializable(typeof(PreviewBudget))]
[JsonSerializable(typeof(List<PreviewRecurring>))]
[JsonSerializable(typeof(PreviewRecurring))]
[JsonSerializable(typeof(CommitMoneyManagerImportResponse))]
[JsonSerializable(typeof(ImportRunCounts))]
[JsonSerializable(typeof(RestoreSnapshotCommand))]
[JsonSerializable(typeof(SnapshotData))]
[JsonSerializable(typeof(List<SnapshotAccount>))]
[JsonSerializable(typeof(SnapshotAccount))]
[JsonSerializable(typeof(List<SnapshotCategory>))]
[JsonSerializable(typeof(SnapshotCategory))]
[JsonSerializable(typeof(List<SnapshotTransaction>))]
[JsonSerializable(typeof(SnapshotTransaction))]
[JsonSerializable(typeof(List<SnapshotParticipant>))]
[JsonSerializable(typeof(SnapshotParticipant))]
[JsonSerializable(typeof(List<SnapshotRepayment>))]
[JsonSerializable(typeof(SnapshotRepayment))]
[JsonSerializable(typeof(List<SnapshotRecurringTransaction>))]
[JsonSerializable(typeof(SnapshotRecurringTransaction))]
[JsonSerializable(typeof(List<SnapshotGoal>))]
[JsonSerializable(typeof(SnapshotGoal))]
[JsonSerializable(typeof(CreatePersonCommand))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(GetPersonsQuery))]
[JsonSerializable(typeof(List<PersonResponse>))]
[JsonSerializable(typeof(PersonResponse))]
[JsonSerializable(typeof(TagDto))]
[JsonSerializable(typeof(List<TagDto>))]
[JsonSerializable(typeof(UpdatePersonCommand))]
[JsonSerializable(typeof(DeletePersonCommand))]
[JsonSerializable(typeof(CreateTagCommand))]
[JsonSerializable(typeof(GetTagsQuery))]
[JsonSerializable(typeof(List<TagResponse>))]
[JsonSerializable(typeof(TagResponse))]
[JsonSerializable(typeof(Dictionary<string, string[]>))]
[JsonSerializable(typeof(List<Guid>))]

// ── EmailImport (slice 13): response DTOs that cross HTTP + the request DTOs bound from
// the same endpoints. Registered so serialization stays reflection-free when AOT is on.
[JsonSerializable(typeof(Budgie.Modules.EmailImport.Application.Connections.EmailAuthorizationUri))]
[JsonSerializable(typeof(Budgie.Modules.EmailImport.Application.Connections.GetConnectionStatus.EmailConnectionStatusResponse))]
[JsonSerializable(typeof(List<Budgie.Modules.EmailImport.Application.Connections.GetConnectionStatus.EmailConnectionStatusResponse>))]
[JsonSerializable(typeof(Budgie.Modules.EmailImport.Application.Rules.EmailImportRuleResponse))]
[JsonSerializable(typeof(List<Budgie.Modules.EmailImport.Application.Rules.EmailImportRuleResponse>))]
[JsonSerializable(typeof(Budgie.Modules.EmailImport.Application.Rules.PreviewRule.PreviewRuleResponse))]
[JsonSerializable(typeof(Budgie.Modules.EmailImport.Application.Inbox.EmailSyncResult))]
[JsonSerializable(typeof(Budgie.Modules.EmailImport.Application.Inbox.GetPending.PendingEmailTransactionResponse))]
[JsonSerializable(typeof(Budgie.Modules.EmailImport.Application.PagedResult<Budgie.Modules.EmailImport.Application.Inbox.GetPending.PendingEmailTransactionResponse>))]
[JsonSerializable(typeof(Budgie.Modules.EmailImport.Application.Inbox.ApproveBatch.ApproveBatchResponse))]
[JsonSerializable(typeof(Budgie.Modules.EmailImport.Presentation.Rules.CreateRuleRequest))]
[JsonSerializable(typeof(Budgie.Modules.EmailImport.Presentation.Rules.UpdateRuleRequest))]
[JsonSerializable(typeof(Budgie.Modules.EmailImport.Presentation.Rules.PreviewRuleRequest))]
[JsonSerializable(typeof(Budgie.Modules.EmailImport.Presentation.Rules.RuleDraftRequest))]
[JsonSerializable(typeof(Budgie.Modules.EmailImport.Presentation.Inbox.ApprovePendingRequest))]
[JsonSerializable(typeof(Budgie.Modules.EmailImport.Presentation.Inbox.ApproveBatchRequest))]
internal partial class BudgieJsonContext : JsonSerializerContext
{
}
