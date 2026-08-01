using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;

namespace Budgie.Modules.Finances.Domain.Goals;

public class Goal : Entity
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid AccountId { get; private set; }
    public Money TargetAmount { get; private set; } = null!;
    public DateTime? TargetDate { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Goal() { }

    public static Goal Create(Guid userId, Guid accountId, decimal targetAmount, string currency, DateTime? targetDate)
    {
        return new Goal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = accountId,
            TargetAmount = Money.FromPositive(targetAmount, CurrencyCode.From(currency)),
            TargetDate = targetDate.HasValue ? GuardDate(targetDate.Value) : null,
            CreatedAt = GuardDate(DateTime.UtcNow)
        };
    }

    public void Update(decimal targetAmount, string currency, DateTime? targetDate)
    {
        TargetAmount = Money.FromPositive(targetAmount, CurrencyCode.From(currency));
        TargetDate = targetDate.HasValue ? GuardDate(targetDate.Value) : null;
    }

    private static DateTime GuardDate(DateTime date) =>
        DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
}
