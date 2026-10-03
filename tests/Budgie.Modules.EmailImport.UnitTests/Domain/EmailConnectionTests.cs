using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Domain;

/// <summary>
/// <see cref="EmailConnection"/> holds the mailbox binding: which provider, which address,
/// an ENCRYPTED credential and an opaque provider cursor. The cursor is the field with the
/// most dangerous failure mode in the module — moving it early would silently skip every
/// unprocessed message — so its transitions are pinned explicitly.
/// </summary>
public class EmailConnectionTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static EmailConnection Create(
        string email = "usuario@banco.es",
        string credentials = "v1:bm9uY2U=:Y2lwaGVy:dGFn",
        EmailProvider provider = EmailProvider.Gmail,
        decimal threshold = EmailConnection.DefaultAutoCommitMinConfidence) =>
        EmailConnection.Create(UserId, provider, email, credentials, threshold);

    // ── Creation ───────────────────────────────────────────────────────────────────────
    [Fact]
    public void Create_NewConnection_StartsEnabledWithNoCursorAndNoError()
    {
        var connection = Create();

        connection.Id.Should().NotBeEmpty();
        connection.UserId.Should().Be(UserId);
        connection.Provider.Should().Be(EmailProvider.Gmail);
        connection.Email.Should().Be("usuario@banco.es");
        connection.SyncEnabled.Should().BeTrue();
        connection.SyncCursor.Should().BeNull();
        connection.LastSyncAt.Should().BeNull();
        connection.LastError.Should().BeNull();
        connection.LastSkippedCount.Should().Be(0);
    }

    [Fact]
    public void Create_DefaultThreshold_IsEightyFiveHundredths()
    {
        Create().AutoCommitMinConfidence.Should().Be(0.85m);
        EmailConnection.DefaultAutoCommitMinConfidence.Should().Be(0.85m);
    }

    [Fact]
    public void Create_EmailIsTrimmed()
    {
        Create(email: "  usuario@banco.es  ").Email.Should().Be("usuario@banco.es");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyEmail_IsRejected(string email)
    {
        var act = () => Create(email: email);
        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("email");
    }

    [Fact]
    public void Create_EmailOverTwoHundredFiftyFiveCharacters_IsRejected()
    {
        var act = () => Create(email: new string('a', 256) + "@banco.es");
        act.Should().Throw<ArgumentException>().Which.Message.Should().Contain("255");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyCredentials_IsRejected(string credentials)
    {
        // A connection without a credential could never read the mailbox; failing at write
        // time is the only place this can be caught cheaply.
        var act = () => Create(credentials: credentials);
        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("encryptedCredentials");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(2.0)]
    public void Create_ThresholdOutsideZeroToOne_IsRejected(decimal threshold)
    {
        var act = () => Create(threshold: threshold);
        act.Should().Throw<ArgumentException>().Which.Message.Should().Contain("between 0 and 1");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.85)]
    [InlineData(1)]
    public void Create_ThresholdAtTheBoundary_IsAccepted(decimal threshold)
    {
        var act = () => Create(threshold: threshold);
        act.Should().NotThrow();
    }

    // ── The cursor ────────────────────────────────────────────────────────────────────
    [Fact]
    public void AdvanceCursor_SetsTheOpaqueCursor()
    {
        var connection = Create();

        connection.AdvanceCursor("r-4711");

        connection.SyncCursor.Should().Be("r-4711");
    }

    [Fact]
    public void AdvanceCursor_NullResetsTheCursorToTheRecoveryWindow()
    {
        // The recovery path after CursorExpired: a dead cursor is worse than no cursor.
        var connection = Create();
        connection.AdvanceCursor("r-4711");

        connection.AdvanceCursor(null);

        connection.SyncCursor.Should().BeNull();
    }

    [Fact]
    public void AdvanceCursor_CursorOverTwoHundredFiftyFiveCharacters_IsTruncated()
    {
        // Truncated, never rejected: the value is opaque and only the provider interprets it.
        var connection = Create();

        connection.AdvanceCursor(new string('c', 300));

        connection.SyncCursor.Should().HaveLength(255);
    }

    // ── Sync outcomes ─────────────────────────────────────────────────────────────────
    [Fact]
    public void RecordSuccess_StoresTheTimestampAndTheSkipCount()
    {
        var connection = Create();
        var now = new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc);

        connection.RecordSuccess(now, skippedCount: 3);

        connection.LastSyncAt.Should().Be(now);
        connection.LastSkippedCount.Should().Be(3);
    }

    [Fact]
    public void RecordSuccess_ClearsAPreviousError()
    {
        var connection = Create();
        connection.RecordError("fallo anterior");

        connection.RecordSuccess(new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc), skippedCount: 0);

        connection.LastError.Should().BeNull();
    }

    [Fact]
    public void RecordError_StoresTheMessageAndDoesNotTouchLastSyncAt()
    {
        // A failed pass is not a sync: LastSyncAt must not pretend one happened.
        var connection = Create();
        connection.RecordSuccess(new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc), skippedCount: 0);

        connection.RecordError("el proveedor rechazó la credencial");

        connection.LastError.Should().Be("el proveedor rechazó la credencial");
        connection.LastSyncAt.Should().Be(new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void RecordError_MessageOverFiveHundredCharacters_IsTruncated()
    {
        var connection = Create();

        connection.RecordError(new string('e', 600));

        connection.LastError.Should().HaveLength(500);
    }

    [Fact]
    public void Disable_TurnsSyncOffButKeepsTheConnection()
    {
        // Never a delete: the user must still be able to see it and reconnect.
        var connection = Create();
        connection.AdvanceCursor("r-1");

        connection.Disable();

        connection.SyncEnabled.Should().BeFalse();
        connection.SyncCursor.Should().Be("r-1");
        connection.Email.Should().Be("usuario@banco.es");
    }

    [Fact]
    public void ChangeAutoCommitThreshold_ValidValue_IsRoundedToTwoDecimals()
    {
        var connection = Create();

        connection.ChangeAutoCommitThreshold(0.8567m);

        connection.AutoCommitMinConfidence.Should().Be(0.86m);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void ChangeAutoCommitThreshold_OutsideZeroToOne_IsRejected(decimal value)
    {
        var connection = Create();

        var act = () => connection.ChangeAutoCommitThreshold(value);

        act.Should().Throw<ArgumentException>().Which.Message.Should().Contain("between 0 and 1");
    }

    [Fact]
    public void ParameterlessConstructor_LeavesEverythingAtItsDefault()
    {
        // The public parameterless constructor exists for Dapper materialization; asserting
        // it does not throw keeps the Dapper path honest.
        var act = () => new EmailConnection();

        act.Should().NotThrow();
        new EmailConnection().Id.Should().BeEmpty();
    }
}
