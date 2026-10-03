using System.Security.Claims;
using Budgie.Modules.EmailImport.Application.Inbox.Abstractions;
using Budgie.Shared.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Budgie.Modules.EmailImport.Infrastructure.Identity;

/// <summary>
/// <see cref="ICurrentUserProvider"/> + <see cref="ICurrentUserProviderAccessor"/> for the
/// whole service tree. Both interfaces resolve to this SAME scoped instance (wired in
/// <see cref="EmailImportModule"/>), which is what lets the flows that run outside an HTTP
/// request — the email sync worker and the manual review dispatch — share one
/// <c>CreateTransactionCommand</c> path with a normal request instead of two
/// implementations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two sources of the current user, in priority order.</b>
/// </para>
/// <list type="number">
/// <item><description>
/// An explicitly pushed user (<see cref="Push"/>): the sync worker pushes each connection's
/// owner around its processing block, and the review dispatch pushes the reviewed row's
/// owner before dispatching. This instance replaced Budgie.Api's HttpContext-only
/// <c>CurrentUserProvider</c> for the whole tree, so outside a request the pushed user is
/// the ONLY valid source — flows that forgot to push fail fast with
/// <see cref="InvalidOperationException"/> instead of silently operating as "no user".
/// </description></item>
/// <item><description>
/// The JWT claim of the current HTTP request, read exactly like the previous
/// <c>Budgie.Api.Infrastructure.CurrentUserProvider</c> did
/// (<c>ClaimTypes.NameIdentifier</c>, then raw <c>sub</c>). This fallback is what keeps
/// every existing endpoint — Finances, Users, Relationships and this module — resolving
/// the same owner as before the replacement.
/// </description></item>
/// </list>
/// <para>
/// The pushed user lives in an <see cref="AsyncLocal{T}"/>: the value flows into every
/// <c>await</c> below it (Mediator handlers, repository calls) without any ambient-state
/// plumbing, and each DI scope owns its own slot — a worker tick and an HTTP request can
/// never see each other's user.
/// </para>
/// </remarks>
internal sealed class ScopedCurrentUserProvider(
    IHttpContextAccessor httpContextAccessor) : ICurrentUserProvider, ICurrentUserProviderAccessor
{
    // One AsyncLocal per scoped instance: scopes are isolated from each other, and the
    // using-dispose pattern restores the previous value even on nested pushes.
    private readonly AsyncLocal<Guid?> _currentUser = new();

    /// <inheritdoc />
    public Guid? UserId => _currentUser.Value ?? ResolveClaimUserId();

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// No user was pushed into this scope AND there is no authenticated HTTP request to
    /// read a claim from. Flows that run outside an HTTP request (the EmailImport sync
    /// worker, manual review dispatch) must wrap their work in
    /// <c>using accessor.Push(userId)</c> so command handlers can resolve the current user.
    /// </exception>
    public Guid GetUserId() => _currentUser.Value ?? ResolveClaimUserId() ?? throw new InvalidOperationException(
        "No user is in scope. Flows that run outside an HTTP request (the EmailImport " +
        "sync worker, manual review dispatch) must wrap their work in " +
        "`using accessor.Push(userId)` so command handlers can resolve the current user.");

    /// <inheritdoc />
    public bool IsAuthenticated =>
        _currentUser.Value.HasValue
        || httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    /// <inheritdoc />
    public IDisposable Push(Guid userId)
    {
        var previous = _currentUser.Value;
        _currentUser.Value = userId;
        return new PushScope(_currentUser, previous);
    }

    /// <summary>
    /// JWT-claim fallback for HTTP requests. Returns <c>null</c> outside a request (worker
    /// scopes) — there the pushed user is the only valid source. Mirrors
    /// <c>Budgie.Api.Infrastructure.CurrentUserProvider</c>: this provider replaced it for
    /// the whole tree, so requests without a pushed user must resolve the owner exactly
    /// like before.
    /// </summary>
    private Guid? ResolveClaimUserId()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user is null)
            return null;

        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(claim, out var userId) ? userId : null;
    }

    /// <summary>
    /// Called by the DI container when the scope ends. The user state lives in the
    /// <see cref="AsyncLocal{T}"/> and is restored by each <see cref="PushScope"/>;
    /// there is nothing else to release.
    /// </summary>
    public void Dispose()
    {
        // Intentionally empty; see XML doc above.
    }

    /// <summary>Restores the previous user on dispose (always used via <c>using</c>).</summary>
    private sealed class PushScope(AsyncLocal<Guid?> slot, Guid? previous) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            // Interlocked: disposing twice (explicit + finalizer-less suppression) must
            // restore the previous value exactly once.
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                slot.Value = previous;
        }
    }
}
