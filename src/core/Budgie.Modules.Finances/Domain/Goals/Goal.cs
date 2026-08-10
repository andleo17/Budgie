using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;

namespace Budgie.Modules.Finances.Domain.Goals;

public class Goal : Entity
{
    public Guid Id { get; internal set; }
    public Guid UserId { get; internal set; }
    public Guid AccountId { get; internal set; }
    public Money TargetAmount { get; internal set; } = null!;
    public DateTime? TargetDate { get; internal set; }
    public DateTime CreatedAt { get; internal set; }

    public Goal() { }

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
