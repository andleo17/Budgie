using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Accounts.GetAccounts;

public record GetAccountsQuery() : IRequest<ErrorOr<List<AccountResponse>>>;
