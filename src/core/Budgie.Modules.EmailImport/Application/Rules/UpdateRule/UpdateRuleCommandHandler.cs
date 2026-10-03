using System.Globalization;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Modules.Finances.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Rules.UpdateRule;

/// <inheritdoc cref="UpdateRuleCommand"/>
/// <remarks>
/// The rule is loaded with <c>(userId, id)</c> so a rule of another user is indistinguishable
/// from a non-existent one (NotFound, no existence leak). The account/category re-validation
/// mirrors CreateRule: an update can retarget the rule at any account, foreign ones included.
/// </remarks>
internal sealed class UpdateRuleCommandHandler(
    IEmailImportRuleRepository ruleRepository,
    IAccountRepository accountRepository,
    ICategoryRepository categoryRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<UpdateRuleCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdateRuleCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var rule = await ruleRepository.GetByIdAsync(userId, request.Id, ct);
        if (rule is null)
            return Error.NotFound("EmailImport.RuleNotFound", "Import rule not found.");

        var account = await accountRepository.GetByIdAsync(request.AccountId, userId, ct);
        if (account is null)
            return Error.NotFound("Account.NotFound", "Account not found.");

        if (request.CategoryId is { } categoryId)
        {
            var category = await categoryRepository.GetByIdAsync(categoryId, userId, ct);
            if (category is null)
                return Error.NotFound("Category.NotFound", "Category not found.");
        }

        try
        {
            // UserId is not a parameter of Update: ownership never changes.
            rule.Update(
                request.Name,
                request.AdapterKey,
                request.Priority,
                request.IsEnabled,
                request.AccountId,
                request.CategoryId,
                request.TransactionType,
                request.Currency,
                request.ExchangeRate,
                request.FromAddress,
                request.SubjectContains,
                request.BodyRegex,
                request.FieldPatternsJson,
                request.AutoCommit);
        }
        catch (ArgumentException ex)
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? Error.Validation("EmailImport.InvalidRule", $"Invalid rule: {ex.Message}")
                : Error.Validation("EmailImport.InvalidRule", $"Regla inválida: {ex.Message}");
        }

        await ruleRepository.UpdateAsync(rule, ct);
        return Result.Success;
    }
}
