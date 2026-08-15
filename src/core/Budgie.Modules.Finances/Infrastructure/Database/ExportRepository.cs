using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Export;
using Budgie.Shared.Infrastructure;
using Dapper;
using Npgsql;

namespace Budgie.Modules.Finances.Infrastructure.Database;

internal sealed class ExportRepository(IDbConnectionFactory factory) : IExportRepository
{
    public async Task<SnapshotData> GetSnapshotAsync(Guid userId, CancellationToken ct)
    {
        var accounts = (await factory.QueryListAsync<SnapshotAccount>(
            @"SELECT ""Id"",""Name"",""Balance"",""Currency"",""Type"",
              ""CreditLimit"",""CreditLimitCurrency"",""CreditConsumptionMode"",""AnnualInterestRate"",
              ""BillingCycleDayInclusive"",""BillingCycleDay"",""PaymentDueDay"",""InterestCalculationMethod"",
              ""ControlStartDate"",""CreatedAt""
              FROM ""Accounts"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct)).ToList();

        var categories = (await factory.QueryListAsync<SnapshotCategory>(
            @"SELECT ""Id"",""Name"",""Color"",""Icon"",""Type"",""IsSystem"",""ParentId"",""MonthlyBudget""
              FROM ""Categories"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct)).ToList();

        var transactions = (await factory.QueryListAsync<SnapshotTransaction>(
            @"SELECT ""Id"",""AccountId"",""CategoryId"",""Amount"",""Currency"",""ExchangeRate"",""Date"",""Notes"",
              ""Type"",""Installments"",""LinkedTransactionId"",""ParentTransactionId"",""IsInitialBalance""
              FROM ""Transactions"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct)).ToList();

        var participants = (await factory.QueryListAsync<SnapshotParticipant>(
            @"SELECT tp.""TransactionId"",""PersonId"",""ContributedAmount"",""ContributedCurrency"",
              ""RemainingAmount"",""RemainingCurrency"",""IsTrackingOnly"",""DestinationAccountId""
              FROM ""TransactionParticipants"" tp
              JOIN ""Transactions"" t ON t.""Id"" = tp.""TransactionId""
              WHERE t.""UserId"" = @UserId",
            new { UserId = userId }, ct)).ToList();

        var repayments = (await factory.QueryListAsync<SnapshotRepayment>(
            @"SELECT rd.""RepaymentTransactionId"",""RepaidParticipantId"",rd.""Amount""
              FROM ""RepaymentDetails"" rd
              JOIN ""Transactions"" t ON t.""Id"" = rd.""RepaymentTransactionId""
              WHERE t.""UserId"" = @UserId",
            new { UserId = userId }, ct)).ToList();

        var recurring = (await factory.QueryListAsync<SnapshotRecurringTransaction>(
            @"SELECT ""Id"",""AccountId"",""CategoryId"",""Name"",""Amount"",""Currency"",""ExchangeRate"",""Notes"",
              ""Type"",""Installments"",""IsInterestFree"",""Frequency"",""CustomIntervalDays"",""StartDate"",""EndDate"",
              ""EndAfterCount"",""EndType"",""Applicability"",""NextDueDate"",""LastAppliedDate"",""TimesApplied"",""IsActive""
              FROM ""RecurringTransactions"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct)).ToList();

        var goals = (await factory.QueryListAsync<SnapshotGoal>(
            @"SELECT ""Id"",""AccountId"",""TargetAmount"",""TargetCurrency"",""TargetDate"",""CreatedAt""
              FROM ""Goals"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct)).ToList();

        return new SnapshotData(
            Version: "1.0",
            ExportedAt: DateTime.UtcNow,
            Accounts: accounts,
            Categories: categories,
            Transactions: transactions,
            Participants: participants,
            Repayments: repayments,
            RecurringTransactions: recurring,
            Goals: goals);
    }

    public async Task RestoreAsync(Guid userId, SnapshotData snapshot, CancellationToken ct)
    {
        using var connection = factory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // Remove existing finance data for this user (safe re-import)
        await connection.ExecuteAsync(@"
            DELETE FROM ""RepaymentDetails"" rd USING ""Transactions"" t WHERE t.""Id"" = rd.""RepaymentTransactionId"" AND t.""UserId"" = @UserId;
            DELETE FROM ""TransactionParticipants"" tp USING ""Transactions"" t WHERE t.""Id"" = tp.""TransactionId"" AND t.""UserId"" = @UserId;
            DELETE FROM ""Transactions"" WHERE ""UserId"" = @UserId;
            DELETE FROM ""Goals"" WHERE ""UserId"" = @UserId;
            DELETE FROM ""RecurringTransactions"" WHERE ""UserId"" = @UserId;
            DELETE FROM ""Categories"" WHERE ""UserId"" = @UserId;
            DELETE FROM ""Accounts"" WHERE ""UserId"" = @UserId;",
            new { UserId = userId }, tx);

        foreach (var a in snapshot.Accounts)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO ""Accounts"" (""Id"",""UserId"",""Name"",""Balance"",""Currency"",""Type"",
                  ""CreditLimit"",""CreditLimitCurrency"",""CreditConsumptionMode"",""AnnualInterestRate"",
                  ""BillingCycleDayInclusive"",""BillingCycleDay"",""PaymentDueDay"",""InterestCalculationMethod"",
                  ""ControlStartDate"",""CreatedAt"")
                  VALUES (@Id,@UserId,@Name,@Balance,@Currency,@Type,
                  @CreditLimit,@CreditLimitCurrency,@CreditConsumptionMode,@AnnualInterestRate,
                  @BillingCycleDayInclusive,@BillingCycleDay,@PaymentDueDay,@InterestCalculationMethod,
                  @ControlStartDate,@CreatedAt)",
                new
                {
                    a.Id, UserId = userId, a.Name, a.Balance, a.Currency, a.Type,
                    a.CreditLimit, a.CreditLimitCurrency, a.CreditConsumptionMode, a.AnnualInterestRate,
                    a.BillingCycleDayInclusive, a.BillingCycleDay, a.PaymentDueDay, a.InterestCalculationMethod,
                    ControlStartDate = a.ControlStartDate == default ? DateTime.UtcNow.Date : a.ControlStartDate,
                    a.CreatedAt
                }, tx);
        }

        foreach (var c in snapshot.Categories)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO ""Categories"" (""Id"",""UserId"",""Name"",""Color"",""Icon"",""Type"",""IsSystem"",""ParentId"",""MonthlyBudget"")
                  VALUES (@Id,@UserId,@Name,@Color,@Icon,@Type,@IsSystem,@ParentId,@MonthlyBudget)",
                new { c.Id, UserId = userId, c.Name, c.Color, c.Icon, c.Type, c.IsSystem, c.ParentId, c.MonthlyBudget }, tx);
        }

        foreach (var t in snapshot.Transactions)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO ""Transactions"" (""Id"",""UserId"",""AccountId"",""CategoryId"",""Amount"",""Currency"",""ExchangeRate"",
                  ""Date"",""Notes"",""Type"",""Installments"",""LinkedTransactionId"",""ParentTransactionId"",""IsInitialBalance"")
                  VALUES (@Id,@UserId,@AccountId,@CategoryId,@Amount,@Currency,@ExchangeRate,
                  @Date,@Notes,@Type,@Installments,@LinkedTransactionId,@ParentTransactionId,@IsInitialBalance)",
                new { t.Id, UserId = userId, t.AccountId, t.CategoryId, t.Amount, t.Currency, t.ExchangeRate,
                      t.Date, t.Notes, t.Type, t.Installments, t.LinkedTransactionId, t.ParentTransactionId, t.IsInitialBalance }, tx);
        }

        foreach (var p in snapshot.Participants)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO ""TransactionParticipants"" (""Id"",""TransactionId"",""PersonId"",""ContributedAmount"",""ContributedCurrency"",
                  ""RemainingAmount"",""RemainingCurrency"",""IsTrackingOnly"",""DestinationAccountId"")
                  VALUES (@Id,@TransactionId,@PersonId,@ContributedAmount,@ContributedCurrency,
                  @RemainingAmount,@RemainingCurrency,@IsTrackingOnly,@DestinationAccountId)",
                new { Id = Guid.NewGuid(), p.TransactionId, p.PersonId, p.ContributedAmount, p.ContributedCurrency,
                      p.RemainingAmount, p.RemainingCurrency, p.IsTrackingOnly, p.DestinationAccountId }, tx);
        }

        foreach (var r in snapshot.Repayments)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO ""RepaymentDetails"" (""RepaymentTransactionId"",""RepaidParticipantId"",""Amount"")
                  VALUES (@RepaymentTransactionId,@RepaidParticipantId,@Amount)",
                new { r.RepaymentTransactionId, r.RepaidParticipantId, r.Amount }, tx);
        }

        foreach (var rt in snapshot.RecurringTransactions)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO ""RecurringTransactions"" (""Id"",""UserId"",""AccountId"",""CategoryId"",""Name"",""Amount"",""Currency"",
                  ""ExchangeRate"",""Notes"",""Type"",""Installments"",""IsInterestFree"",""Frequency"",""CustomIntervalDays"",
                  ""StartDate"",""EndDate"",""EndAfterCount"",""EndType"",""Applicability"",""NextDueDate"",""LastAppliedDate"",""TimesApplied"",""IsActive"")
                  VALUES (@Id,@UserId,@AccountId,@CategoryId,@Name,@Amount,@Currency,
                  @ExchangeRate,@Notes,@Type,@Installments,@IsInterestFree,@Frequency,@CustomIntervalDays,
                  @StartDate,@EndDate,@EndAfterCount,@EndType,@Applicability,@NextDueDate,@LastAppliedDate,@TimesApplied,@IsActive)",
                new { rt.Id, UserId = userId, rt.AccountId, rt.CategoryId, rt.Name, rt.Amount, rt.Currency,
                      rt.ExchangeRate, rt.Notes, rt.Type, rt.Installments, rt.IsInterestFree, rt.Frequency, rt.CustomIntervalDays,
                      rt.StartDate, rt.EndDate, rt.EndAfterCount, rt.EndType, rt.Applicability, rt.NextDueDate, rt.LastAppliedDate, rt.TimesApplied, rt.IsActive }, tx);
        }

        foreach (var g in snapshot.Goals)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO ""Goals"" (""Id"",""UserId"",""AccountId"",""TargetAmount"",""TargetCurrency"",""TargetDate"",""CreatedAt"")
                  VALUES (@Id,@UserId,@AccountId,@TargetAmount,@TargetCurrency,@TargetDate,@CreatedAt)",
                new { g.Id, UserId = userId, g.AccountId, g.TargetAmount, g.TargetCurrency, g.TargetDate, g.CreatedAt }, tx);
        }

        await tx.CommitAsync(ct);
    }
}
