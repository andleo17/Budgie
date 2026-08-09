using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Accounts.DeleteAccount;

public record DeleteAccountCommand(Guid Id) : IRequest<ErrorOr<Success>>;
