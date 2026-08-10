using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Currencies;
using Budgie.Modules.Finances.Domain.Goals;
using Budgie.Modules.Finances.Domain.RecurringTransactions;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Infrastructure.Database;

public class FinancesDbContext(
    DbContextOptions<FinancesDbContext> options,
    ICurrentUserProvider currentUserProvider) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionParticipant> TransactionParticipants => Set<TransactionParticipant>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
    public DbSet<RecurringTransactionParticipant> RecurringTransactionParticipants => Set<RecurringTransactionParticipant>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp without time zone");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExchangeRate>(builder =>
        {
            builder.HasKey(er => er.Id);
            builder.Property(er => er.FromCurrency).HasConversion(ValueConverters.CurrencyCodeConverter).IsRequired().HasMaxLength(3);
            builder.Property(er => er.ToCurrency).HasConversion(ValueConverters.CurrencyCodeConverter).IsRequired().HasMaxLength(3);
            builder.Property(er => er.Rate).HasColumnType("decimal(18,4)").IsRequired();
            builder.Property(er => er.Date).IsRequired();
        });

        modelBuilder.Entity<Account>(builder =>
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.UserId).IsRequired();
            builder.Property(a => a.Name).HasConversion(ValueConverters.NameConverter).IsRequired().HasMaxLength(100);
            builder.Property(a => a.Type).IsRequired();

            builder.OwnsOne(a => a.Balance, b =>
            {
                b.Property(m => m.Amount).HasColumnName("Balance").HasColumnType("decimal(18,2)").IsRequired();
                b.Property(m => m.Currency).HasColumnName("Currency").HasConversion(ValueConverters.CurrencyCodeConverter).IsRequired().HasMaxLength(3);
            });

            builder.OwnsOne(a => a.CreditCardSettings, c =>
            {
                c.OwnsOne(cc => cc.CreditLimit, m =>
                {
                    m.Property(x => x.Amount).HasColumnName("CreditLimit").HasColumnType("decimal(18,2)").IsRequired();
                    m.Property(x => x.Currency).HasColumnName("CreditLimitCurrency").HasConversion(ValueConverters.CurrencyCodeConverter).IsRequired().HasMaxLength(3);
                });
                c.Property(cc => cc.CreditConsumptionMode);
                c.Property(cc => cc.AnnualInterestRate).HasConversion(ValueConverters.PercentageConverter).HasColumnType("decimal(18,4)");
                c.Property(cc => cc.BillingCycleDayInclusive).HasDefaultValue(true);
            });

            builder.HasQueryFilter(a => a.UserId == currentUserProvider.UserId);
        });

        modelBuilder.Entity<Goal>(builder =>
        {
            builder.HasKey(g => g.Id);
            builder.Property(g => g.UserId).IsRequired();
            builder.Property(g => g.AccountId).IsRequired();

            builder.OwnsOne(g => g.TargetAmount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("TargetAmount").HasColumnType("decimal(18,2)").IsRequired();
                m.Property(x => x.Currency).HasColumnName("TargetCurrency").HasConversion(ValueConverters.CurrencyCodeConverter).IsRequired().HasMaxLength(3);
            });

            builder.Property(g => g.TargetDate);

            builder.HasQueryFilter(g => g.UserId == currentUserProvider.UserId);
        });

        modelBuilder.Entity<Category>(builder =>
        {
            builder.HasKey(c => c.Id);
            builder.Property(c => c.UserId);
            builder.Property(c => c.Name).HasConversion(ValueConverters.NameConverter).IsRequired().HasMaxLength(50);
            builder.Property(c => c.Color).HasConversion(ValueConverters.NullableHexColorConverter).HasMaxLength(7);
            builder.Property(c => c.Icon).HasMaxLength(50);
            builder.Property(c => c.Type).IsRequired();

            builder.HasOne(c => c.Parent)
                   .WithMany(c => c.Children)
                   .HasForeignKey(c => c.ParentId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasQueryFilter(c => c.UserId == currentUserProvider.UserId || c.IsSystem);
        });

        modelBuilder.Entity<Transaction>(builder =>
        {
            builder.HasKey(t => t.Id);
            builder.Property(t => t.UserId).IsRequired();

            builder.OwnsOne(t => t.Amount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
                m.Property(x => x.Currency).HasColumnName("Currency").HasConversion(ValueConverters.CurrencyCodeConverter).IsRequired().HasMaxLength(3);
            });

            builder.Property(t => t.ExchangeRate).HasColumnType("decimal(18,4)").IsRequired();
            builder.Property(t => t.Date).IsRequired();
            builder.Property(t => t.Notes).HasMaxLength(500);
            builder.Property(t => t.RepaidParticipants).HasMaxLength(4000);
            builder.Property(t => t.Installments).HasDefaultValue(1);
            builder.Property(t => t.LinkedTransactionId);
            builder.Property(t => t.ParentTransactionId);
            builder.Property(t => t.IsInitialBalance).HasDefaultValue(false);
            builder.Property(t => t.InterestRate).HasConversion(ValueConverters.PercentageConverter);

            builder.HasOne(t => t.Account)
                   .WithMany()
                   .HasForeignKey(t => t.AccountId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(t => t.Category)
                   .WithMany()
                   .HasForeignKey(t => t.CategoryId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(t => t.ParentTransaction)
                   .WithMany(t => t.ChildTransactions)
                   .HasForeignKey(t => t.ParentTransactionId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasQueryFilter(t => t.UserId == currentUserProvider.UserId);

            var participantsNav = builder.Metadata.FindNavigation(nameof(Transaction.Participants));
            participantsNav?.SetPropertyAccessMode(PropertyAccessMode.Field);

            var childrenNav = builder.Metadata.FindNavigation(nameof(Transaction.ChildTransactions));
            childrenNav?.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<TransactionParticipant>(builder =>
        {
            builder.HasKey(tp => tp.Id);

            builder.OwnsOne(tp => tp.ContributedAmount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("ContributedAmount").HasColumnType("decimal(18,2)").IsRequired();
                m.Property(x => x.Currency).HasColumnName("ContributedCurrency").HasConversion(ValueConverters.CurrencyCodeConverter).IsRequired().HasMaxLength(3);
            });

            builder.OwnsOne(tp => tp.RemainingAmount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("RemainingAmount").HasColumnType("decimal(18,2)").HasDefaultValue(0);
                m.Property(x => x.Currency).HasColumnName("RemainingCurrency").HasConversion(ValueConverters.CurrencyCodeConverter).IsRequired().HasMaxLength(3);
            });

            builder.Property(tp => tp.IsTrackingOnly).HasDefaultValue(false);
            builder.Property(tp => tp.DestinationAccountId);

            builder.HasOne(tp => tp.Transaction)
                   .WithMany(t => t.Participants)
                   .HasForeignKey(tp => tp.TransactionId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(tp => tp.DestinationAccount)
                   .WithMany()
                   .HasForeignKey(tp => tp.DestinationAccountId)
                   .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RecurringTransaction>(builder =>
        {
            builder.HasKey(r => r.Id);
            builder.Property(r => r.UserId).IsRequired();
            builder.Property(r => r.Name).HasConversion(ValueConverters.NameConverter).IsRequired().HasMaxLength(100);

            builder.OwnsOne(r => r.Amount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
                m.Property(x => x.Currency).HasColumnName("Currency").HasConversion(ValueConverters.CurrencyCodeConverter).IsRequired().HasMaxLength(3);
            });

            builder.Property(r => r.ExchangeRate).HasColumnType("decimal(18,4)").IsRequired();
            builder.Property(r => r.Notes).HasMaxLength(500);
            builder.Property(r => r.Installments).HasDefaultValue(1);
            builder.Property(r => r.IsInterestFree).HasDefaultValue(true);
            builder.Property(r => r.CustomIntervalDays);
            builder.Property(r => r.EndAfterCount);
            builder.Property(r => r.TimesApplied).HasDefaultValue(0);
            builder.Property(r => r.IsActive).HasDefaultValue(true);

            builder.HasOne(r => r.Account)
                   .WithMany()
                   .HasForeignKey(r => r.AccountId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(r => r.Category)
                   .WithMany()
                   .HasForeignKey(r => r.CategoryId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasQueryFilter(r => r.UserId == currentUserProvider.UserId);

            var participantsNav = builder.Metadata.FindNavigation(nameof(RecurringTransaction.Participants));
            participantsNav?.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<RecurringTransactionParticipant>(builder =>
        {
            builder.HasKey(rp => rp.Id);

            builder.OwnsOne(rp => rp.ContributedAmount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("ContributedAmount").HasColumnType("decimal(18,2)").IsRequired();
                m.Property(x => x.Currency).HasColumnName("ContributedCurrency").HasConversion(ValueConverters.CurrencyCodeConverter).IsRequired().HasMaxLength(3);
            });

            builder.OwnsOne(rp => rp.RemainingAmount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("RemainingAmount").HasColumnType("decimal(18,2)").HasDefaultValue(0);
                m.Property(x => x.Currency).HasColumnName("RemainingCurrency").HasConversion(ValueConverters.CurrencyCodeConverter).IsRequired().HasMaxLength(3);
            });

            builder.Property(rp => rp.IsTrackingOnly).HasDefaultValue(false);
            builder.Property(rp => rp.DestinationAccountId);

            builder.HasOne(rp => rp.RecurringTransaction)
                   .WithMany(r => r.Participants)
                   .HasForeignKey(rp => rp.RecurringTransactionId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(rp => rp.DestinationAccount)
                   .WithMany()
                   .HasForeignKey(rp => rp.DestinationAccountId)
                   .OnDelete(DeleteBehavior.SetNull);
        });

        base.OnModelCreating(modelBuilder);
    }
}
