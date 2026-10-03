using System.Globalization;
using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Application.Parsing;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Modules.Finances.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Rules.PreviewRule;

/// <inheritdoc cref="PreviewRuleCommand"/>
/// <remarks>
/// The preview is strictly read-only: no staging row, no rule, no transaction. Both modes
/// converge on the same pipeline — resolve rule → validate targets → extract plain text →
/// adapter — so a draft previews exactly like the rule will behave once saved.
/// </remarks>
internal sealed class PreviewRuleCommandHandler(
    IEmailImportRuleRepository ruleRepository,
    IAccountRepository accountRepository,
    ICategoryRepository categoryRepository,
    IMessageTextExtractor textExtractor,
    IBankAdapterRegistry adapterRegistry,
    ICurrentUserProvider currentUserProvider,
    ILanguageProvider? languageProvider = null)
    : IRequestHandler<PreviewRuleCommand, ErrorOr<PreviewRuleResponse>>
{
    public async ValueTask<ErrorOr<PreviewRuleResponse>> Handle(PreviewRuleCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        // ── 1. Resolve the rule under test ────────────────────────────────────────────
        EmailImportRule rule;
        if (request.RuleId is { } ruleId)
        {
            // Saved-rule mode. Loaded with (userId, id): a foreign rule is a NotFound.
            var existing = await ruleRepository.GetByIdAsync(userId, ruleId, ct);
            if (existing is null)
                return Error.NotFound("EmailImport.RuleNotFound", "Import rule not found.");
            rule = existing;
        }
        else if (request.Draft is { } draft)
        {
            // Draft mode. Built in memory with IsEnabled = true: the user is testing the
            // patterns, not the activation flag. Domain validation runs here exactly as it
            // will at save time — an invalid draft fails the preview with the very message
            // the user needs to see, before anything can be persisted.
            try
            {
                rule = EmailImportRule.Create(
                    userId,
                    draft.Name,
                    draft.AdapterKey,
                    draft.Priority,
                    isEnabled: true,
                    draft.AccountId,
                    draft.CategoryId,
                    draft.TransactionType,
                    draft.Currency,
                    draft.ExchangeRate,
                    draft.FromAddress,
                    draft.SubjectContains,
                    draft.BodyRegex,
                    draft.FieldPatternsJson,
                    draft.AutoCommit);
            }
            catch (ArgumentException ex)
            {
                return InvalidRule(ex);
            }
        }
        else
        {
            return Error.Validation(
                "EmailImport.PreviewRuleMissing",
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                    ? "Provide either ruleId (saved rule) or draft (rule being edited) to preview."
                    : "Indica ruleId (regla guardada) o draft (regla en edición) para previsualizar.");
        }

        // ── 2. Validate targets exactly like CreateRule ───────────────────────────────
        // A draft may point at an account that does not exist or belongs to another user;
        // the preview must reject it the same way Create would, not discover it at sync.
        var account = await accountRepository.GetByIdAsync(rule.AccountId, userId, ct);
        if (account is null)
            return Error.NotFound("Account.NotFound", "Account not found.");

        if (rule.CategoryId is { } categoryId)
        {
            var category = await categoryRepository.GetByIdAsync(categoryId, userId, ct);
            if (category is null)
                return Error.NotFound("Category.NotFound", "Category not found.");
        }

        // ── 3. Synthetic message from what the user pasted ────────────────────────────
        var message = EmailMessage.Create(
            "preview",
            threadId: null,
            request.FromAddress,
            request.Subject,
            request.RawEmail,
            DateTimeOffset.UtcNow);

        // ── 4. Plain text through the same extractor the sync uses ────────────────────
        var plainText = textExtractor.ExtractPlainText(message);

        // ── 5. The rule's adapter ─────────────────────────────────────────────────────
        IBankAdapter adapter;
        try
        {
            adapter = adapterRegistry.Get(rule.AdapterKey);
        }
        catch (InvalidOperationException ex)
        {
            // The domain cannot know which adapters are registered (that is Infrastructure's
            // wiring); a draft naming an unregistered adapter surfaces here instead of 500.
            return Error.Validation("EmailImport.UnknownAdapter", ex.Message);
        }

        var languageCode = ResolveLanguageCode();
        var outcome = adapter.Parse(new EmailParseContext(message, plainText, rule, languageCode));

        // ── 6/7. Report: plain text always, fields only on a match ────────────────────
        if (!outcome.Matched)
        {
            // Skip, not error: this is exactly what the user must see to fix the patterns.
            return new PreviewRuleResponse(
                plainText,
                new Dictionary<string, string?>(StringComparer.Ordinal),
                new Dictionary<string, decimal>(StringComparer.Ordinal),
                Confidence: 0m,
                new List<string>(),
                outcome.SkipReason);
        }

        var value = outcome.Value!;
        var fields = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["amount"] = value.Amount.ToString(CultureInfo.InvariantCulture),
            ["currency"] = value.Currency,
            ["date"] = value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["merchant"] = value.Merchant,
            ["type"] = value.TransactionType.ToString()
        };

        return new PreviewRuleResponse(
            plainText,
            fields,
            value.FieldConfidence,
            value.Confidence,
            ConfidenceScorer.Explain(value.FieldConfidence, languageCode),
            SkipReason: null);
    }

    /// <summary>
    /// User language for number/date normalization and scorer explanations. Falls back to
    /// <c>es</c> when no language provider is wired (e.g. before settings exist): a preview
    /// must never fail over a missing preference.
    /// </summary>
    private string ResolveLanguageCode()
    {
        var code = languageProvider?.LanguageCode;
        return string.IsNullOrWhiteSpace(code) ? "es" : code;
    }

    /// <inheritdoc cref="CreateRule.CreateRuleCommandHandler"/> pattern: domain message carried into a localized error.
    private static Error InvalidRule(ArgumentException ex) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
            ? Error.Validation("EmailImport.InvalidRule", $"Invalid rule: {ex.Message}")
            : Error.Validation("EmailImport.InvalidRule", $"Regla inválida: {ex.Message}");
}
