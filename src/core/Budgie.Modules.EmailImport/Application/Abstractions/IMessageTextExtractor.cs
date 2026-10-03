using Budgie.Modules.EmailImport.Domain.Parsing;

namespace Budgie.Modules.EmailImport.Application.Abstractions;

/// <summary>
/// Port that turns an <see cref="EmailMessage"/> into the plain text the adapters match their
/// regexes against. The MIME/HTML machinery lives in Infrastructure (<c>RawEmailReader</c> and
/// <c>HtmlToTextConverter</c>, orchestrated by <c>MessageTextExtractor</c>) because Application
/// must never depend on it: <c>GenericRegexAdapter</c> consumes the normalizers and needs this
/// port, not the implementation.
/// </summary>
public interface IMessageTextExtractor
{
    /// <summary>
    /// Returns the plain-text rendering of the message body. If the body is raw MIME source,
    /// transfer encodings and multipart parts are decoded first; if it is HTML, it is converted
    /// to structured plain text (tabs between cells, newlines between blocks). Plain text that
    /// is neither MIME nor HTML is returned unchanged.
    /// </summary>
    /// <param name="message">The provider-neutral message to render.</param>
    string ExtractPlainText(EmailMessage message);
}
