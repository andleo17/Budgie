using System.Globalization;
using ErrorOr;

namespace Budgie.Modules.EmailImport.Application.Connections;

/// <summary>
/// The <see cref="Error"/>s the connection surface answers with, defined once.
/// </summary>
/// <remarks>
/// These were literals duplicated per handler, which is how two messages that must stay in sync
/// drift apart: "there is no mailbox connected for this provider" is the answer to several
/// unrelated commands, and a user comparing two screens should not see two phrasings of the same
/// fact. Every string is localized through the repo's
/// <c>CultureInfo.CurrentUICulture</c> convention.
/// </remarks>
internal static class EmailImportErrors
{
    /// <summary>
    /// No mailbox is connected for the requested provider. Shared by disconnect and by the
    /// auto-commit threshold update, which both act on one concrete connection row.
    /// </summary>
    internal static Error NotConnected() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
            ? Error.NotFound("EmailImport.NotConnected", "There is no mailbox connected for this provider.")
            : Error.NotFound("EmailImport.NotConnected", "No hay ningún buzón conectado para este proveedor.");

    /// <summary>
    /// The auto-commit threshold is outside [0, 1].
    /// </summary>
    /// <remarks>
    /// Deliberately a fixed message, not the domain's <c>ArgumentException.Message</c>: that text
    /// ends in <c>(Parameter 'value')</c>, so a Spanish user would read a .NET parameter name
    /// inside a translated sentence. The precise domain message is for logs, not for the UI.
    /// </remarks>
    internal static Error InvalidThreshold() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
            ? Error.Validation(
                "EmailImport.InvalidThreshold",
                "The auto-commit threshold must be a number between 0 and 1.")
            : Error.Validation(
                "EmailImport.InvalidThreshold",
                "El umbral de auto-commit debe ser un número entre 0 y 1.");
}