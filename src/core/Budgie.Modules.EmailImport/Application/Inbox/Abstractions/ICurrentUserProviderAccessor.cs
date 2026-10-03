namespace Budgie.Modules.EmailImport.Application.Inbox.Abstractions;

/// <summary>
/// Supplies the <c>UserId</c> of the flow currently in progress, outside of an HTTP request.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> <c>CreateTransactionCommandHandler</c> (Finances) resolves its
/// owner through <see cref="Budgie.Shared.Abstractions.ICurrentUserProvider"/>, which in a
/// web request reads the JWT from <c>HttpContext</c>. The email sync worker is a
/// <c>BackgroundService</c>: there is no <c>HttpContext</c> at all, yet its auto-commit path
/// must dispatch the exact same command — the spec (§5.2) forbids a second, divergent
/// "create imported transaction" implementation that would bypass <c>ControlStartDate</c>,
/// installment math, participants and <c>TransactionCreatedEvent</c>.
/// </para>
/// <para>
/// <see cref="Push(Guid)"/> fixes the current user for the duration of the returned
/// disposable, so the worker and the manual review route (slice 12) share one code path.
/// Resolving <c>ICurrentUserProvider</c> in a flow that forgot to push fails fast with
/// <see cref="InvalidOperationException"/> instead of silently operating as "no user".
/// </para>
/// </remarks>
public interface ICurrentUserProviderAccessor : IDisposable
{
    /// <summary>
    /// Sets <paramref name="userId"/> as the current user until the returned
    /// <see cref="IDisposable"/> is disposed. Always use with <c>using</c> so the
    /// previous value (usually none) is restored even when the flow throws.
    /// </summary>
    IDisposable Push(Guid userId);
}
