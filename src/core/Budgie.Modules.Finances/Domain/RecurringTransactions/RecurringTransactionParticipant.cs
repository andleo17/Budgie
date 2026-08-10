using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Shared.Domain.ValueObjects;

namespace Budgie.Modules.Finances.Domain.RecurringTransactions;

public class RecurringTransactionParticipant
{
    public Guid Id { get; internal set; }
    public Guid RecurringTransactionId { get; internal set; }
    public Guid PersonId { get; internal set; }
    public Money ContributedAmount { get; internal set; } = null!;
    public Money RemainingAmount { get; internal set; } = null!;
    public bool IsTrackingOnly { get; internal set; }
    public Guid? DestinationAccountId { get; internal set; }
    public RecurringTransaction RecurringTransaction { get; internal set; } = null!;
    public Account? DestinationAccount { get; internal set; }

    public RecurringTransactionParticipant() { }

    public static RecurringTransactionParticipant Create(Guid personId, decimal contributedAmount, CurrencyCode currency, decimal remainingAmount = 0, bool isTrackingOnly = false, Guid? destinationAccountId = null)
    {
        return new RecurringTransactionParticipant
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            ContributedAmount = Money.FromNonNegative(contributedAmount, currency),
            RemainingAmount = Money.FromNonNegative(remainingAmount, currency),
            IsTrackingOnly = isTrackingOnly,
            DestinationAccountId = destinationAccountId
        };
    }
}
