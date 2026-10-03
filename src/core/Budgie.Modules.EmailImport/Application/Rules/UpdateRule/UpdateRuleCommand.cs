using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Rules.UpdateRule;

/// <summary>
/// Updates an existing import rule of the current user. Same shape as
/// <see cref="CreateRule.CreateRuleCommand"/> plus <c>Id</c>; <c>UserId</c> is never part of
/// the request — ownership is resolved server-side and never changes.
/// </summary>
public record UpdateRuleCommand(
    Guid Id,
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
    bool AutoCommit) : IRequest<ErrorOr<Success>>;
