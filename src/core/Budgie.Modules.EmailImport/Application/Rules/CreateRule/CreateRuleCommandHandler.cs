using System.Globalization;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Modules.Finances.Application;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Rules.CreateRule;

/// <inheritdoc cref="CreateRuleCommand"/>
/// <remarks>
/// The account (and category, when present) is verified to exist <b>and belong to the current
/// user</b> before the domain factory runs: a rule pointing at a foreign or deleted account
/// must fail here with a clear 404, not at sync time when the transaction is created.
/// </remarks>
internal sealed class CreateRuleCommandHandler(
    IEmailImportRuleRepository ruleRepository,
    IAccountRepository accountRepository,
    ICategoryRepository categoryRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<CreateRuleCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreateRuleCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var account = await accountRepository.GetByIdAsync(request.AccountId, userId, ct);
        if (account is null)
            return Error.NotFound("Account.NotFound", "Account not found.");

        if (request.CategoryId is { } categoryId)
        {
            var category = await categoryRepository.GetByIdAsync(categoryId, userId, ct);
            if (category is null)
                return Error.NotFound("Category.NotFound", "Category not found.");
        }

        EmailImportRule rule;
        try
        {
            rule = EmailImportRule.Create(
                userId,
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
            return InvalidRule(ex);
        }

        await ruleRepository.AddAsync(rule, ct);
        return rule.Id;
    }

    /// <summary>
    /// Maps a domain invariant violation to a localized validation error. The domain message
    /// (English, precise about the violated invariant) is carried along so the rule form can
    /// show the user exactly which field failed and why.
    /// </summary>
    private static Error InvalidRule(ArgumentException ex) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
            ? Error.Validation("EmailImport.InvalidRule", $"Invalid rule: {ex.Message}")
            : Error.Validation("EmailImport.InvalidRule", $"Regla inválida: {ex.Message}");
}
