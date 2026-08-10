using System.Text.Json.Serialization;
using Budgie.Modules.Finances.Application.Accounts;
using Budgie.Modules.Finances.Application.Accounts.CreateAccount;
using Budgie.Modules.Finances.Application.Accounts.GetAccountById;
using Budgie.Modules.Finances.Application.Accounts.GetAccounts;
using Budgie.Modules.Finances.Application.Categories.GetCategories;
using Budgie.Modules.Finances.Application.Debts.GetPersonDebts;
using Budgie.Modules.Finances.Application.Persons.GetPersonSummary;
using Budgie.Modules.Finances.Application.Transactions;
using Budgie.Modules.Finances.Application.Transactions.BulkRepay;
using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Modules.Finances.Application.Transactions.DeleteTransaction;
using Budgie.Modules.Finances.Application.Transactions.GetTransactions;
using Budgie.Modules.Finances.Presentation.Accounts;
using Budgie.Modules.Relationships.Application.Persons;
using Budgie.Modules.Relationships.Application.Persons.CreatePerson;
using Budgie.Modules.Relationships.Application.Persons.DeletePerson;
using Budgie.Modules.Relationships.Application.Persons.GetPersons;
using Budgie.Modules.Relationships.Application.Persons.UpdatePerson;
using Budgie.Modules.Relationships.Application.Tags;
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
[JsonSerializable(typeof(CreateTransactionCommand))]
[JsonSerializable(typeof(TransactionParticipantDto))]
[JsonSerializable(typeof(List<TransactionParticipantDto>))]
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
[JsonSerializable(typeof(GetCategoriesQuery))]
[JsonSerializable(typeof(List<CategoryResponse>))]
[JsonSerializable(typeof(CategoryResponse))]
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
internal partial class BudgieJsonContext : JsonSerializerContext
{
}
