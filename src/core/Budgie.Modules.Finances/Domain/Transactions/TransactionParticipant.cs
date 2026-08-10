using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Shared.Domain.ValueObjects;

namespace Budgie.Modules.Finances.Domain.Transactions;

public class TransactionParticipant
{
    public Guid Id { get; internal set; }
    public Guid TransactionId { get; internal set; }
    public Guid PersonId { get; internal set; }
    public Money ContributedAmount { get; internal set; } = null!;
    public Money RemainingAmount { get; internal set; } = null!;
    public bool IsTrackingOnly { get; internal set; }
    public Guid? DestinationAccountId { get; internal set; }
    public Transaction Transaction { get; internal set; } = null!;
    public Account? DestinationAccount { get; internal set; }

    public TransactionParticipant() { }

    public static TransactionParticipant Create(Guid personId, decimal contributedAmount, CurrencyCode currency, decimal remainingAmount = 0, bool isTrackingOnly = false, Guid? destinationAccountId = null)
    {
        return new TransactionParticipant
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            ContributedAmount = Money.FromNonNegative(contributedAmount, currency),
            RemainingAmount = Money.FromNonNegative(remainingAmount, currency),
            IsTrackingOnly = isTrackingOnly,
            DestinationAccountId = destinationAccountId
        };
    }

    public void ReduceRemaining(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        if (amount > RemainingAmount.Amount)
            throw new ArgumentException("Amount exceeds remaining amount.", nameof(amount));

        RemainingAmount = Money.FromNonNegative(RemainingAmount.Amount - amount, RemainingAmount.Currency);
    }

    public void RestoreRemaining(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        RemainingAmount = Money.FromNonNegative(RemainingAmount.Amount + amount, RemainingAmount.Currency);
    }
}
