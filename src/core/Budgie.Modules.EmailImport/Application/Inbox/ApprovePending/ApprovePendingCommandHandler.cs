using System.Globalization;
using Budgie.Modules.EmailImport.Application.Inbox.Abstractions;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Modules.EmailImport.Domain.Staging;
using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using Budgie.Shared.Exceptions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Inbox.ApprovePending;

/// <inheritdoc cref="ApprovePendingCommand"/>
internal sealed class ApprovePendingCommandHandler(
    IPendingEmailTransactionRepository pendingRepository,
    IEmailImportRuleRepository ruleRepository,
    IEmailConnectionRepository connectionRepository,
    IAccountRepository accountRepository,
    ICategoryRepository categoryRepository,
    ITransactionRepository transactionRepository,
    IMediator mediator,
    ICurrentUserProvider currentUserProvider,
    ICurrentUserProviderAccessor currentUserAccessor)
    : IRequestHandler<ApprovePendingCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(ApprovePendingCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var pending = await pendingRepository.GetByIdAsync(userId, request.Id, ct);

        // A row that is gone, or no longer Pending (already committed by the auto-commit
        // worker, dismissed, failed), cannot be approved: NotFound, never a second
        // transaction. This Status guard is the FIRST re-approval barrier.
        if (pending is null || pending.Status != PendingEmailStatus.Pending)
            return PendingApprovalHelper.PendingNotFound();

        var context = new PendingApprovalContext(
            pendingRepository, ruleRepository, connectionRepository,
            accountRepository, categoryRepository, transactionRepository,
            mediator, currentUserAccessor);

        return await PendingApprovalHelper.ApproveAsync(userId, pending, request, context, ct);
    }
}

/// <summary>
/// Dependency bundle of the shared approval path, so the single-row handler and the batch
/// handler run the exact same code with the exact same capabilities.
/// </summary>
internal sealed record PendingApprovalContext(
    IPendingEmailTransactionRepository PendingRepository,
    IEmailImportRuleRepository RuleRepository,
    IEmailConnectionRepository ConnectionRepository,
    IAccountRepository AccountRepository,
    ICategoryRepository CategoryRepository,
    ITransactionRepository TransactionRepository,
    IMediator Mediator,
    ICurrentUserProviderAccessor Accessor);

/// <summary>
/// THE approval path of the review inbox. <c>ApprovePendingCommandHandler</c> and
/// <c>ApproveBatchCommandHandler</c> both call it — one implementation of the dispatch, the
/// idempotency barriers and the row state transitions (spec §5.2, §3.5, §8.1). Extracted
/// from the single-row handler so the batch can never grow a divergent copy.
/// </summary>
internal static class PendingApprovalHelper
{
    public static async Task<ErrorOr<Guid>> ApproveAsync(
        Guid userId,
        PendingEmailTransaction pending,
        ApprovePendingCommand request,
        PendingApprovalContext context,
        CancellationToken ct)
    {
        // FIRST re-approval barrier: a row that is not Pending is never dispatched again.
        // The single-row handler already checked this right after loading; the batch path
        // listed its rows earlier, so the check here also covers a row that flipped to
        // Committed/Dismissed between listing and approval.
        if (pending.Status != PendingEmailStatus.Pending)
            return PendingNotFound();

        // The rule is the source of the target account, currency and exchange rate (spec
        // §3.2: every rule declares a fixed account). It may be gone (RuleId is ON DELETE
        // SET NULL) — then the user MUST override the account explicitly.
        EmailImportRule? rule = null;
        if (pending.RuleId is { } ruleId)
            rule = await context.RuleRepository.GetByIdAsync(userId, ruleId, ct);

        // User overrides are verified to exist AND belong to the current user BEFORE any
        // write: approving with a foreign account must fail with NotFound and create
        // nothing (same codes as CreateRuleCommandHandler).
        if (request.AccountId is { } accountIdOverride)
        {
            var account = await context.AccountRepository.GetByIdAsync(accountIdOverride, userId, ct);
            if (account is null)
                return Error.NotFound("Account.NotFound", "Account not found.");
        }

        if (request.CategoryId is { } categoryIdOverride)
        {
            var category = await context.CategoryRepository.GetByIdAsync(categoryIdOverride, userId, ct);
            if (category is null)
                return Error.NotFound("Category.NotFound", "Category not found.");
        }

        // Target account: the user's override wins; otherwise the rule's. Without either
        // there is nothing to dispatch to — fail fast and visibly.
        if (request.AccountId is not { } accountId || accountId == Guid.Empty)
        {
            if (rule?.AccountId is not { } ruleAccountId || ruleAccountId == Guid.Empty)
                return AccountRequired();
            accountId = ruleAccountId;
        }

        var categoryId = request.CategoryId ?? rule?.CategoryId;

        // Corrections are optional: approve-without-touching uses the staged values as-is
        // and never rewrites the row.
        var hasCorrections = request.Amount is not null || request.Date is not null ||
            request.Merchant is not null || request.Notes is not null ||
            request.AccountId is not null || request.CategoryId is not null ||
            request.Type is not null;

        if (hasCorrections)
        {
            try
            {
                pending.ApplyReview(
                    request.Amount ?? pending.Amount.Amount,
                    // Staged currency/rate, not the rule's: see the note at the dispatch.
                    pending.Currency.Code,
                    pending.ExchangeRate,
                    request.Date ?? pending.Date,
                    request.Merchant ?? pending.Merchant,
                    request.Notes ?? pending.Notes,
                    accountId,
                    categoryId,
                    request.Type ?? pending.TransactionType);
            }
            catch (ArgumentException ex)
            {
                return InvalidReview(ex);
            }

            // Persists the corrections; Status stays Pending — committing happens below.
            await context.PendingRepository.UpdateReviewedAsync(pending, ct);
        }

        // ── The SECOND idempotency barrier key (spec §3.5). MUST be byte-for-byte the same
        // string the sync worker builds in EmailSyncService:
        //   $"email:{provider}:{messageId}"   (provider lowercased, e.g. "email:gmail:18f2…")
        // A different format would let the same email be imported twice — once by the
        // worker's auto-commit and once by this manual approval — because the unique index
        // IX_Transactions_UserId_ExternalUid would never see the collision.
        var connection = await context.ConnectionRepository.GetByIdAsync(userId, pending.ConnectionId, ct);
        if (connection is null)
            return ConnectionNotFound();

        var externalUid =
            $"email:{connection.Provider.ToString().ToLowerInvariant()}:{pending.MessageId}";

        // ── Dispatch through Mediator (spec §5.2), never through ITransactionRepository:
        // that is what inherits ControlStartDate validation, installment math, participants
        // and the TransactionCreatedEvent that moves the account balance.
        // The Push is MANDATORY: CreateTransactionCommand resolves its owner from
        // ICurrentUserProvider, and outside the worker's per-connection scope nobody has
        // pushed this user yet — without it GetUserId() throws inside the Finances handler.
        using var userScope = context.Accessor.Push(userId);

        // Notes carries the REVIEWED merchant (correction or original) — unlike slice 11's
        // auto-commit, which deliberately sends null: here a human vouched for the row.
        // Null merchant → null Notes.
        ErrorOr<Guid> result;
        try
        {
            result = await context.Mediator.Send(
                new CreateTransactionCommand(
                    AccountId: accountId,
                    CategoryId: categoryId,
                    Amount: pending.Amount.Amount,
                    // The currency and the rate come from the REVIEWED ROW, never from the
                    // rule: the rule can be edited between staging and approval, and then
                    // the amount the user reviewed against EUR would be dispatched as USD.
                    // The row is the artifact the user actually vetted.
                    Currency: pending.Currency.Code,
                    ExchangeRate: pending.ExchangeRate,
                    Date: pending.Date,
                    Notes: pending.Merchant,
                    Type: pending.TransactionType,
                    ExternalUid: externalUid),
                ct);
        }
        catch (UniqueConstraintViolationException)
        {
            // SECOND barrier hit: the transaction ALREADY exists — most likely the worker's
            // auto-commit raced this approval, or a previous attempt died between dispatch
            // and MarkCommitted. That is a success, not an error: link the row to the
            // existing transaction, exactly like EmailSyncService does.
            //
            // The exception is the neutral one TransactionRepository translates out of the
            // driver's, so Application never references Npgsql (AGENTS.md).
            var existingId = await context.TransactionRepository.GetIdByExternalUidAsync(
                userId, externalUid, ct);

            if (existingId is null)
            {
                // Defensive race (same as the worker): it existed at insert time but is
                // gone now. Surface it as Failed — never invent a link.
                var lostMessage = BuildLostTransactionMessage();
                await context.PendingRepository.MarkFailedAsync(
                    userId, pending.Id, lostMessage, DateTime.UtcNow, ct);
                return Error.Conflict("EmailImport.TransactionLost", lostMessage);
            }

            await context.PendingRepository.MarkCommittedAsync(
                userId, pending.Id, existingId.Value, DateTime.UtcNow, ct);
            return existingId.Value;
        }

        if (result.IsError)
        {
            // Mark the row Failed with the very description the UI shows (e.g.
            // Transaction.BeforeControlStartDate) AND return the error to the caller. A
            // row left Pending here would silently fail again on the next sync (plan §12).
            await context.PendingRepository.MarkFailedAsync(
                userId, pending.Id, result.Errors[0].Description, DateTime.UtcNow, ct);
            return result;
        }

        await context.PendingRepository.MarkCommittedAsync(
            userId, pending.Id, result.Value, DateTime.UtcNow, ct);
        return result.Value;
    }

    /// <summary>
    /// The one definition of the "row is not approvable" error, shared with
    /// <c>DismissPendingCommandHandler</c> so the code and the es/en copy stay in sync.
    /// </summary>
    internal static Error PendingNotFound() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
            ? Error.NotFound(
                "EmailImport.PendingNotFound",
                "Pending email transaction not found or already reviewed.")
            : Error.NotFound(
                "EmailImport.PendingNotFound",
                "La transacción de correo pendiente no existe o ya fue revisada.");

    private static Error AccountRequired() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
            ? Error.Validation(
                "EmailImport.AccountRequired",
                "This pending row has no target account (its rule was deleted). Choose an account to approve it.")
            : Error.Validation(
                "EmailImport.AccountRequired",
                "Esta fila pendiente no tiene cuenta de destino (su regla fue eliminada). Elige una cuenta para aprobarla.");

    private static Error InvalidReview(ArgumentException ex) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
            ? Error.Validation("EmailImport.InvalidReview", $"Invalid review: {ex.Message}")
            : Error.Validation("EmailImport.InvalidReview", $"Revisión inválida: {ex.Message}");

    private static Error ConnectionNotFound() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
            ? Error.NotFound(
                "EmailImport.ConnectionNotFound",
                "The mailbox connection that staged this row no longer exists.")
            : Error.NotFound(
                "EmailImport.ConnectionNotFound",
                "La conexión de buzón que creó esta fila ya no existe.");

    private static string BuildLostTransactionMessage() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
            ? "The transaction already existed (imported earlier) but could not be found afterwards; review this row manually."
            : "La transacción ya existía (importada antes) pero no se encontró después; revisa esta fila manualmente.";
}
