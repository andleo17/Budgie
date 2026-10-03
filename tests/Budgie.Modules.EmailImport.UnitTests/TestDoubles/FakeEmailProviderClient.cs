using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Application.Connections;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Shared.Enums;

namespace Budgie.Modules.EmailImport.UnitTests.TestDoubles;

/// <summary>
/// Scriptable <see cref="IEmailProviderClient"/>: no network, no vendor. Every member is a
/// settable delegate so a test expresses only the behaviour it cares about and every other
/// member keeps a harmless default instead of throwing.
/// </summary>
internal sealed class FakeEmailProviderClient : IEmailProviderClient
{
    public EmailProvider Provider { get; set; } = EmailProvider.Gmail;

    /// <summary>Messages handed back by <see cref="ListNewAsync"/>.</summary>
    public List<EmailMessage> Messages { get; } = [];

    /// <summary>
    /// Failures thrown by successive <see cref="ListNewAsync"/> calls, dequeued one per
    /// call. Modelling it as a QUEUE rather than a single flag is what makes the
    /// cursor-expired recovery testable: the first call fails, the retry succeeds.
    /// </summary>
    public Queue<EmailProviderException> ListFailures { get; } = [];

    /// <summary>Fails every <see cref="ListNewAsync"/> call with this error.</summary>
    public EmailProviderException? ListFailure
    {
        get => ListFailures.Count > 0 ? ListFailures.Peek() : null;
        set
        {
            ListFailures.Clear();
            if (value is not null) ListFailures.Enqueue(value);
        }
    }

    /// <summary>Number of <see cref="ListNewAsync"/> calls. Used to assert the cursor-expired
    /// recovery ran exactly one retry (and not an unbounded loop).</summary>
    public int ListCallCount { get; private set; }

    /// <summary>The cursors the connection carried at each <see cref="ListNewAsync"/> call, in
    /// order: <c>[null, "cursor-after-retry"]</c> proves the retry really ran with a null cursor.</summary>
    public List<string?> CursorsSeenAtList { get; } = [];

    /// <summary>Cursor the fake advances <em>in memory</em> on the connection, mirroring what a
    /// real provider does while listing. This is what the sync service is expected to persist.</summary>
    public string? CursorToEstablish { get; set; }

    /// <summary>Value returned by <see cref="GetCurrentCursorAsync"/> (re-anchor probe).</summary>
    public string? CurrentCursor { get; set; }

    /// <summary>Credential handed back by <see cref="ExchangeCodeAsync"/>.</summary>
    public EmailCredentials ExchangeResult { get; set; } =
        new("access-token-clear-text", "v1:bm9uY2U=:Y2lwaGVy:dGFn", DateTimeOffset.UtcNow.AddHours(1));

    public string? ExchangeFailure { get; set; }

    /// <summary>The <c>state</c> the last <see cref="BuildAuthorizationUriAsync"/> received, so
    /// tests can complete the OAuth callback with the very state the module issued.</summary>
    public string? LastState { get; private set; }

    public string? LastRedirectUri { get; private set; }

    public string? LastCode { get; private set; }

    public Task<EmailAuthorizationUri> BuildAuthorizationUriAsync(
        EmailConnection connection, string redirectUri, string state, CancellationToken ct)
    {
        LastState = state;
        LastRedirectUri = redirectUri;
        return Task.FromResult(new EmailAuthorizationUri(
            $"https://provider.test/authorize?state={Uri.EscapeDataString(state)}", state));
    }

    public Task<EmailCredentials> ExchangeCodeAsync(
        string encryptedCredentials, string code, string redirectUri, CancellationToken ct)
    {
        LastCode = code;
        if (ExchangeFailure is not null)
            throw new EmailProviderException(EmailProviderError.Unauthorized, ExchangeFailure);
        return Task.FromResult(ExchangeResult);
    }

    public Task<EmailCredentials> RefreshAsync(string encryptedCredentials, CancellationToken ct) =>
        Task.FromResult(ExchangeResult);

    public Task<IReadOnlyList<EmailMessage>> ListNewAsync(
        EmailConnection connection, CancellationToken ct)
    {
        ListCallCount++;
        CursorsSeenAtList.Add(connection.SyncCursor);
        if (ListFailures.Count > 0)
            throw ListFailures.Dequeue();

        // A real provider may establish the new cursor in memory while listing; the sync
        // service is the one that persists it once the whole batch has been processed.
        if (CursorToEstablish is not null)
            connection.AdvanceCursor(CursorToEstablish);

        return Task.FromResult<IReadOnlyList<EmailMessage>>([.. Messages]);
    }

    public Task<string?> GetCurrentCursorAsync(EmailConnection connection, CancellationToken ct) =>
        Task.FromResult(CurrentCursor);

    public Task<EmailMessage> GetAsync(
        EmailConnection connection, string messageId, CancellationToken ct) =>
        Task.FromResult(Messages.First(m => m.MessageId == messageId));
}
