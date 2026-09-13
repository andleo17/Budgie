using FluentAssertions;
using System.IO;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Infrastructure;

/// <summary>
/// Regression guard for UI layout bug: formularios movidos hacia abajo.
/// DialogContent had gap-4 + inner py-4 = 32px top offset. Fix uses gap-0
/// so header->body is only py-4 (16px). This test prevents reintroducing gap-4.
/// </summary>
public class DialogLayoutRegressionTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "src/apps/Budgie.Web/src/components/ui/dialog.tsx")))
        {
            // Walk up until we find repo root (contains src/)
            dir = dir.Parent;
            if (dir == null) break;
            // second check for file existence via relative path
            if (File.Exists(Path.Combine(dir.FullName, "src/apps/Budgie.Web/src/components/ui/dialog.tsx")))
                break;
        }
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    [Fact]
    public void DialogContent_ShouldNotHaveGap4_ShouldUseGap0()
    {
        var root = FindRepoRoot();
        var dialogPath = Path.Combine(root, "src/apps/Budgie.Web/src/components/ui/dialog.tsx");
        File.Exists(dialogPath).Should().BeTrue($"dialog.tsx should exist at {dialogPath}");

        var content = File.ReadAllText(dialogPath);

        // The fixed version must contain gap-0 for both mobile and desktop
        content.Should().Contain("gap-0", "DialogContent gap-4 pushes forms down (header py-4 + gap-4 = 32px). Fixed uses gap-0");
        // Should not contain the old double-gap pattern on the Popup class (gap-4 without gap-0)
        // Allow gap-0 to be present, but gap-4 should only appear if it's sm:gap-0 or similar
        // We check that the main Popup class does not contain ' w-full gap-4 ' (old bug)
        content.Should().NotContain("w-full gap-4 overflow", "Old DialogContent used 'w-full gap-4 overflow' which caused forms to appear moved down");
    }
}
