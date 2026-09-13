using FluentAssertions;
using System.IO;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Infrastructure;

/// <summary>
/// Guards for date picker day preservation and filtered month default date.
/// </summary>
public class DateAndFilterRegressionTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src/apps/Budgie.Web/src/components/ui/date-time-picker.tsx");
            if (File.Exists(candidate)) return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    [Fact]
    public void DateTimePicker_ShouldPreserveDayOnMonthChange()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "src/apps/Budgie.Web/src/components/ui/date-time-picker.tsx");
        File.Exists(path).Should().BeTrue();

        var content = File.ReadAllText(path);
        // Must have handleMonthChange that preserves day via Math.min(selectedDate.getDate(), daysInMonth)
        content.Should().Contain("handleMonthChange", "DateTimePicker must handle month/year navigation");
        content.Should().Contain("Math.min(selectedDate.getDate(), daysInMonth)", "Must clamp day to daysInMonth to preserve selected day like native input");
        content.Should().Contain("onMonthChange={handleMonthChange}", "Calendar must be wired to handleMonthChange");
        // Must preserve timeStr as well
        content.Should().Contain("timeStr", "Must preserve time when changing month/year");
    }

    [Fact]
    public void DatePreservation_ShouldClampCorrectly()
    {
        // Pure logic test for the preservation algorithm (mirrors handleMonthChange)
        static string PreserveDay(DateTime selected, DateTime targetMonth, string timeStr)
        {
            var y = targetMonth.Year;
            var m = targetMonth.Month - 1;
            var daysInMonth = DateTime.DaysInMonth(y, m + 1);
            var d = Math.Min(selected.Day, daysInMonth);
            return $"{y:D4}-{(m + 1):D2}-{d:D2}T{timeStr}";
        }

        // Jan 31 -> Feb 2026 (28 days) => Feb 28
        PreserveDay(new DateTime(2026, 1, 31), new DateTime(2026, 2, 1), "10:00:00")
            .Should().Be("2026-02-28T10:00:00");

        // Jan 31 -> Feb 2024 leap (29 days) => Feb 29
        PreserveDay(new DateTime(2024, 1, 31), new DateTime(2024, 2, 1), "00:00:00")
            .Should().Be("2024-02-29T00:00:00");

        // Mar 15 -> Apr 15 => keep 15
        PreserveDay(new DateTime(2026, 3, 15), new DateTime(2026, 4, 1), "12:30:00")
            .Should().Be("2026-04-15T12:30:00");

        // May 31 -> Jun 30 => Jun 30
        PreserveDay(new DateTime(2026, 5, 31), new DateTime(2026, 6, 1), "00:00:00")
            .Should().Be("2026-06-30T00:00:00");

        // Feb 28 (from Jan 31 clamp) -> Mar => Mar 28 (native stays 28, not restore 31)
        PreserveDay(new DateTime(2026, 2, 28), new DateTime(2026, 3, 1), "00:00:00")
            .Should().Be("2026-03-28T00:00:00");
    }

    [Fact]
    public void NewTransaction_ShouldDefaultToFilteredMonthFirstDay()
    {
        var root = FindRepoRoot();

        // Transactions page must compute initialDate from dateRange.from and pass to dialog
        var transactionsPage = Path.Combine(root, "src/apps/Budgie.Web/src/modules/transactions/pages/Transactions.tsx");
        File.Exists(transactionsPage).Should().BeTrue();
        var txContent = File.ReadAllText(transactionsPage);
        txContent.Should().Contain("initialDate", "Transactions page must pass initialDate to dialog");
        txContent.Should().Contain("dateRange.from", "initialDate must be derived from filtered period's first day");
        txContent.Should().Contain("TransactionFormDialog", "Must render TransactionFormDialog with initialDate");

        // Dialog must forward initialDate to all forms
        var dialogPath = Path.Combine(root, "src/apps/Budgie.Web/src/modules/transactions/components/TransactionFormDialog.tsx");
        var dialogContent = File.ReadAllText(dialogPath);
        dialogContent.Should().Contain("initialDate", "Dialog must accept and forward initialDate");
        dialogContent.Should().Contain("IncomeExpenseForm", "Must forward to IncomeExpenseForm");
        dialogContent.Should().Contain("TransferForm", "Must forward to TransferForm");
        dialogContent.Should().Contain("LoanForm", "Must forward to LoanForm");
        dialogContent.Should().Contain("CollectionPaymentForm", "Must forward to CollectionPaymentForm");

        // IncomeExpenseForm must use initialDate for new transactions
        var incomePath = Path.Combine(root, "src/apps/Budgie.Web/src/modules/transactions/components/IncomeExpenseForm.tsx");
        var incomeContent = File.ReadAllText(incomePath);
        incomeContent.Should().Contain("initialDate", "IncomeExpenseForm must accept initialDate prop");
        incomeContent.Should().Contain("initialDate ?? formatLocal", "Must fallback to today when no filter, but use filtered month's first day when present");

        var transferPath = Path.Combine(root, "src/apps/Budgie.Web/src/modules/transactions/components/TransferForm.tsx");
        var transferContent = File.ReadAllText(transferPath);
        transferContent.Should().Contain("initialDate", "TransferForm must accept initialDate");

        var loanPath = Path.Combine(root, "src/apps/Budgie.Web/src/modules/transactions/components/LoanForm.tsx");
        var loanContent = File.ReadAllText(loanPath);
        loanContent.Should().Contain("initialDate", "LoanForm must accept initialDate");
    }
}
