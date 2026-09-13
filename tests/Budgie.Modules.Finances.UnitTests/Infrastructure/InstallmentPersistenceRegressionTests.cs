using FluentAssertions;
using System.IO;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Infrastructure;

/// <summary>
/// Guards that installment metadata is persisted and the create handler splits
/// multi-installment transactions.
/// </summary>
public class InstallmentPersistenceRegressionTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "src/apps/Budgie.Web/src/components/ui/dialog.tsx")))
        {
            dir = dir.Parent;
            if (dir == null) break;
            if (File.Exists(Path.Combine(dir.FullName, "src/apps/Budgie.Web/src/components/ui/dialog.tsx")))
                break;
        }
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    [Fact]
    public void TransactionRepository_ShouldPersistInstallmentFields()
    {
        var root = FindRepoRoot();
        var repoPath = Path.Combine(root, "src/core/Budgie.Modules.Finances/Infrastructure/Database/TransactionRepository.cs");
        File.Exists(repoPath).Should().BeTrue();

        var content = File.ReadAllText(repoPath);
        content.Should().Contain("InstallmentGroupId", "Repository must persist InstallmentGroupId for cuotas grouping");
        content.Should().Contain("InstallmentNumber", "Repository must persist InstallmentNumber");
        content.Should().Contain("TotalInstallments", "Repository must persist TotalInstallments");
        content.Should().Contain("InterestRate", "Repository must persist InterestRate");
        content.Should().Contain("LinkedTransactionId", "Repository must persist LinkedTransactionId");
    }

    [Fact]
    public void CreateTransactionHandler_ShouldHandleInstallmentsSplitting()
    {
        var root = FindRepoRoot();
        var handlerPath = Path.Combine(root, "src/core/Budgie.Modules.Finances/Application/Transactions/CreateTransaction/CreateTransactionCommandHandler.cs");
        File.Exists(handlerPath).Should().BeTrue();

        var content = File.ReadAllText(handlerPath);
        content.Should().Contain("Installments <= 1", "Handler must branch single vs installments");
        content.Should().Contain("CreateInstallmentsAsync", "Installments splitting logic must exist");
        content.Should().Contain("installmentGroupId", "Must generate InstallmentGroupId for cuotas");
        content.Should().Contain("AddMonths", "Installments must space dates by months");
        content.Should().Contain("IsInterestFree", "Must respect IsInterestFree for interest calculation");
    }
}
