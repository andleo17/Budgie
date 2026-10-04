using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Inbox.ApprovePending;

/// <summary>
/// Approves one staged row into a real Finances transaction (spec §5.2, §8.1). Every field
/// except <see cref="Id"/> is optional: approving without touching anything uses the values
/// exactly as staged; each non-null field overrides the corresponding staged value.
/// </summary>
/// <remarks>
/// The dispatch reuses the SAME idempotency path as the sync auto-commit — the same
/// <c>ExternalUid = "email:{provider}:{messageId}"</c> key and the same
/// <see cref="Budgie.Shared.Exceptions.UniqueConstraintViolationException"/> interpretation — so approving a row the worker already committed
/// can never create a second transaction.
/// </remarks>
/// <param name="Id">Staging row id.</param>
/// <param name="Amount">Optional amount override (must be positive).</param>
/// <param name="Date">Optional transaction date override.</param>
/// <param name="Merchant">Optional merchant override; it becomes the transaction Notes.</param>
/// <param name="Notes">Optional user notes persisted on the staging row.</param>
/// <param name="AccountId">Optional target account override; must exist and belong to the user.</param>
/// <param name="CategoryId">Optional category override; must exist and belong to the user.</param>
/// <param name="Type">Optional Income/Expense override.</param>
public record ApprovePendingCommand(
    Guid Id,
    decimal? Amount = null,
    DateTime? Date = null,
    string? Merchant = null,
    string? Notes = null,
    Guid? AccountId = null,
    Guid? CategoryId = null,
    TransactionType? Type = null) : IRequest<ErrorOr<Guid>>;
