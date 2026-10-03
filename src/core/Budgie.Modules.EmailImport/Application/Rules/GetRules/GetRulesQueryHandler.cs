using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Rules.GetRules;

/// <inheritdoc cref="GetRulesQuery"/>
/// <remarks>
/// Priority ascending is the rule-selection order (spec §4.3): the first rule whose filters
/// match wins. The UI lists them in the same order they will be evaluated, so the user can
/// see which rule answers for an email before any preview runs.
/// </remarks>
internal sealed class GetRulesQueryHandler(
    IEmailImportRuleRepository ruleRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetRulesQuery, ErrorOr<List<EmailImportRuleResponse>>>
{
    public async ValueTask<ErrorOr<List<EmailImportRuleResponse>>> Handle(GetRulesQuery request, CancellationToken ct)
    {
        var rules = await ruleRepository.GetByUserAsync(currentUserProvider.GetUserId(), ct);

        return rules
            .OrderBy(rule => rule.Priority)
            .Select(ToResponse)
            .ToList();
    }

    private static EmailImportRuleResponse ToResponse(EmailImportRule rule) => new(
        rule.Id,
        rule.Name,
        rule.AdapterKey,
        rule.Priority,
        rule.IsEnabled,
        rule.AccountId,
        rule.CategoryId,
        rule.TransactionType,
        rule.Currency,
        rule.ExchangeRate,
        rule.FromAddress,
        rule.SubjectContains,
        rule.BodyRegex,
        rule.FieldPatternsJson,
        rule.AutoCommit);
}
