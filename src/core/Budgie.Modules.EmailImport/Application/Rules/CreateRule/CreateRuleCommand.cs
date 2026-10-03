using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Rules.CreateRule;

/// <summary>
/// Creates an import rule for the current user. <c>UserId</c> is intentionally absent: it is
/// taken from <c>ICurrentUserProvider</c>, never from the request body, so a caller cannot
/// create rules on behalf of another user.
/// </summary>
/// <remarks>
/// Invariants (transaction type, adapter requirements, priority, regex compilation) are
/// validated by the domain factory <c>EmailImportRule.Create</c>; the handler converts its
/// <see cref="ArgumentException"/> into <c>Error.Validation("EmailImport.InvalidRule")</c>.
/// </remarks>
public record CreateRuleCommand(
    string Name,
    string AdapterKey,
    int Priority,
    bool IsEnabled,
    Guid AccountId,
    Guid? CategoryId,
    TransactionType TransactionType,
    string Currency,
    decimal? ExchangeRate,
    string? FromAddress,
    string? SubjectContains,
    string? BodyRegex,
    string? FieldPatternsJson,
    bool AutoCommit) : IRequest<ErrorOr<Guid>>;
