using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Shared.Domain.ValueObjects;

namespace Budgie.Modules.Finances.Domain.RecurringTransactions;

public class RecurringTransactionParticipant
{
    public Guid Id { get; private set; }
    public Guid RecurringTransactionId { get; internal set; }
    public Guid PersonId { get; private set; }
    public Money ContributedAmount { get; private set; } = null!;
    public Money RemainingAmount { get; private set; } = null!;
    public bool IsTrackingOnly { get; private set; }
    public Guid? DestinationAccountId { get; private set; }
    public RecurringTransaction RecurringTransaction { get; private set; } = null!;
    public Account? DestinationAccount { get; private set; }

    private RecurringTransactionParticipant() { }

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
