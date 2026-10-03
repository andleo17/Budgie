namespace Budgie.Modules.EmailImport.UnitTests.TestDoubles;

/// <summary>
/// Manually advanced <see cref="TimeProvider"/>. <c>OAuthStateService</c> takes the clock
/// through its constructor (the production overload feeds it <see cref="TimeProvider.System"/>),
/// so injecting this is the only way to test the 10-minute state window without sleeping
/// for ten minutes. No test-time package is needed: <see cref="TimeProvider"/> is in the
/// BCL since .NET 8.
/// </summary>
public sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public TestTimeProvider(DateTimeOffset? start = null) =>
        _utcNow = start ?? new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    /// <summary>Moves the clock forward. The state window is 10 minutes, so a jump of
    /// 11 minutes is the smallest delta that makes an issued state expired.</summary>
    public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
}
