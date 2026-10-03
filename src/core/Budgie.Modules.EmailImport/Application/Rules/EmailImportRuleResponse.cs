using Budgie.Shared.Enums;

namespace Budgie.Modules.EmailImport.Application.Rules;

/// <summary>
/// Read model of an <see cref="Domain.Rules.EmailImportRule"/> returned by the rules API.
/// Mirrors the entity exactly: no transformation, no hidden fields (there is nothing secret
/// in a rule — credentials live in connections, never here).
/// </summary>
/// <remarks>
/// <c>FieldPatternsJson</c> is echoed back as stored. Note that PostgreSQL normalizes jsonb
/// on write (key order, escaping), so clients must round-trip the parsed JSON, never compare
/// raw strings.
/// </remarks>
public record EmailImportRuleResponse(
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
    bool AutoCommit);
