using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Accounts.GetAccountById;

public record GetAccountByIdQuery(Guid Id) : IRequest<ErrorOr<AccountResponse>>;
