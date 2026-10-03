using Budgie.Modules.EmailImport.Domain.Parsing;

namespace Budgie.Modules.EmailImport.Application.Parsing;

/// <summary>
/// Port for a parser that turns one email (rendered to plain text) plus one import rule into
/// a <see cref="ParseOutcome"/>. Implementations are stateless and registered as singletons.
/// </summary>
/// <remarks>
/// There is deliberately no <c>CanHandle</c> method: adapter selection belongs to the rule,
/// via <c>EmailImportRule.AdapterKey</c>. The registry is therefore a trivial key lookup and
/// adding a bank adapter never means teaching every other adapter about it.
/// </remarks>
public interface IBankAdapter
{
    /// <summary>
    /// Registry key of this adapter. Must match the <c>AdapterKey</c> of the rules that use it
    /// (v1 ships only <c>generic.regex</c>).
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Parses <paramref name="context.PlainText"/> with the regex patterns declared in
    /// <c>context.Rule.FieldPatternsJson"/> and scores the confidence per field.
    /// Returns <see cref="ParseOutcome.NoMatch(string)"/> with a machine-readable skip reason
    /// (e.g. <c>amount-not-found</c>) when nothing can be extracted — an amount is never invented.
    /// </summary>
    ParseOutcome Parse(EmailParseContext context);
}
