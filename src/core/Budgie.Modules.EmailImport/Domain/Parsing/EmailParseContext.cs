using Budgie.Modules.EmailImport.Domain.Rules;

namespace Budgie.Modules.EmailImport.Domain.Parsing;

/// <summary>
/// Everything an adapter needs to parse one email with one rule: the provider-neutral message,
/// its plain-text rendering (produced once per message and reused across rules), the winning
/// rule, and the user's language code, which determines number and date formats.
/// </summary>
/// <param name="Message">The email being parsed.</param>
/// <param name="PlainText">Plain-text body; adapters match their regexes against this.</param>
/// <param name="Rule">The import rule selected for this message.</param>
/// <param name="LanguageCode">Two-letter language code ("es", "en") from <c>ILanguageProvider</c>.</param>
public sealed record EmailParseContext(
    EmailMessage Message,
    string PlainText,
    EmailImportRule Rule,
    string LanguageCode);
