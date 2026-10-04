namespace Budgie.Shared.Exceptions;

/// <summary>
/// A database unique constraint was violated, reported without leaking the driver's exception
/// type across a layer boundary.
/// </summary>
/// <remarks>
/// <para>
/// Infrastructure translates its driver's unique-violation exception into this one at the single
/// point where the INSERT happens, so Application never has to reference
/// <c>Npgsql.PostgresException</c> and inspect its <c>SqlState</c> — which AGENTS.md forbids
/// ("Application: must not contain Npgsql").
/// </para>
/// <para>
/// <b>It deliberately does NOT inherit from <see cref="InvalidOperationException"/></b>, unlike
/// its sibling <c>Finances/Application/Import/InvalidImportFileException</c>. That one inherits
/// from it to stay compatible with pre-existing <c>catch</c> blocks. Here there is no such
/// compatibility to preserve, and inheriting would be actively dangerous: a
/// <c>catch (InvalidOperationException)</c> anywhere on the call path would swallow this
/// exception, and for the email-import idempotency barrier that swallow is not a crash — it is
/// a transaction that looks failed and gets retried forever.
/// </para>
/// <para>
/// It says a unique constraint was violated, not WHICH one. Deciding that a violation of
/// <c>Transactions."ExternalUid"</c> means "already imported" is the caller's job, not the
/// repository's.
/// </para>
/// <para>
/// <b>No <c>SqlState</c> property, on purpose.</b> The implementation plan asked for one. It is
/// omitted deliberately: the value is always <c>23505</c> by construction (the type only exists to
/// translate that one case), so exposing it would invite a caller to branch on it — reintroducing
/// exactly the driver-coupled decision this exception exists to remove. The driver's own message
/// is preserved in <see cref="Exception.Message"/> and the original exception in
/// <see cref="Exception.InnerException"/>, so nothing is lost for diagnostics.
/// </para>
/// </remarks>
public sealed class UniqueConstraintViolationException : Exception
{
    /// <param name="message">The driver's message, preserved for logging.</param>
    public UniqueConstraintViolationException(string message) : base(message)
    {
    }

    /// <param name="message">The driver's message, preserved for logging.</param>
    /// <param name="innerException">The infrastructure exception this one translates.</param>
    public UniqueConstraintViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}