using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Enums;
using Budgie.Shared.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Budgie.IntegrationTests;

/// <summary>
/// Slice 7 (§3.1): <c>TransactionRepository.AddAsync</c> translates the driver's unique-violation
/// into <see cref="UniqueConstraintViolationException"/>, which is what lets the EmailImport
/// idempotency barrier stay in Application without Application referencing Npgsql.
/// </summary>
/// <remarks>
/// These run against a real PostgreSQL container on purpose. The thing being tested is a
/// translation of a driver exception, so faking the driver exception would test the fake and
/// nothing else — exactly the mistake the debt spec §9.1 warns about. The user and account are
/// created through the HTTP API so the foreign keys are real too.
/// </remarks>
public class UniqueConstraintTranslationTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly BudgieWebApplicationFactory _factory;

    public UniqueConstraintTranslationTests(BudgieWebApplicationFactory factory) => _factory = factory;

    /// <summary>
    /// Inserts one transaction through the repository itself, so the INSERT — and therefore the
    /// constraint it trips — is the real one rather than a hand-written statement.
    /// </summary>
    private static Task InsertAsync(
        IServiceProvider services,
        Guid userId,
        Guid accountId,
        string? externalUid) =>
        RunScopeAsync(services, async repository =>
        {
            var transaction = Transaction.Create(
                userId, accountId, null, 10m, "EUR", 1m, new DateTime(2026, 10, 2),
                null, TransactionType.Expense, externalUid: externalUid);

            await repository.AddAsync(transaction, CancellationToken.None);
        });

    private static async Task RunScopeAsync(
        IServiceProvider services, Func<ITransactionRepository, Task> action)
    {
        using var scope = services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<ITransactionRepository>());
    }

    [Fact]
    public async Task AddAsync_DuplicateExternalUid_ThrowsTheNeutralException()
    {
        var client = _factory.CreateClient();
        await AuthHelper.RegisterAndLoginAsync(client);
        var userId = ReadUserIdFromToken(client);
        var accountId = await AuthHelper.CreateAccountAsync(client, "Para traducción", "EUR", 0m);

        var externalUid = $"email:gmail:{Guid.NewGuid()}";
        await InsertAsync(_factory.Services, userId, accountId, externalUid);

        var act = () => InsertAsync(_factory.Services, userId, accountId, externalUid);

        // NOT PostgresException: that is the whole point of the slice. And NOT
        // InvalidOperationException either — a generic catch of that type would swallow the
        // idempotency barrier and retry the import forever.
        await act.Should().ThrowAsync<UniqueConstraintViolationException>(
            "a duplicate ExternalUid is what the EmailImport barrier keys on");

        await act.Should().NotThrowAsync<InvalidOperationException>(
            "inheriting InvalidOperationException would let a generic catch swallow the barrier");
    }

    [Fact]
    public async Task AddAsync_NonCollidingInsertWithNoExternalUid_PassesThroughUntranslated()
    {
        var client = _factory.CreateClient();
        await AuthHelper.RegisterAndLoginAsync(client);
        var userId = ReadUserIdFromToken(client);
        var accountId = await AuthHelper.CreateAccountAsync(client, "Para traducción", "EUR", 0m);

        // Same account, twice, no ExternalUid: nothing collides, so this proves the new catch does not
        // turn ordinary inserts into exceptions. Two transactions with a null ExternalUid do not
        // collide, so both succeed.
        var act = () => InsertAsync(_factory.Services, userId, accountId, null);
        await act.Should().NotThrowAsync();
        await act.Should().NotThrowAsync<UniqueConstraintViolationException>();
    }

    // Note on coverage: the catch is keyed on SqlState 23505 alone, not on the index name, so it is
    // not ExternalUid-specific by construction. It is not reachable from this project for a
    // SECOND unique constraint: "Transactions" has exactly one non-primary-key unique index
    // (IX_Transactions_UserId_ExternalUid), and "Transaction.Id" has an internal setter that
    // Budgie.IntegrationTests cannot reach. The foreign-key test below is what actually pins
    // the boundary of the translation.

    [Fact]
    public async Task AddAsync_ForeignKeyViolation_IsNotTranslated()
    {
        var client = _factory.CreateClient();
        await AuthHelper.RegisterAndLoginAsync(client);
        var userId = ReadUserIdFromToken(client);

        var missingAccountId = Guid.NewGuid();

        var act = () => InsertAsync(_factory.Services, userId, missingAccountId, null);

        // SqlState 23503, a different failure entirely. Without this test, "optimising" the
        // translation into a blanket catch would make EmailImport swallow referential errors as
        // if they were idempotency.
        var thrown = await act.Should().ThrowAsync<PostgresException>(
            "only unique violations are translated; everything else keeps the driver's type");
        thrown.Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    private static Guid ReadUserIdFromToken(HttpClient client)
    {
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        var payload = token.Split('.')[1];
        var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')
            .Replace('-', '+').Replace('_', '/');

        using var document = System.Text.Json.JsonDocument.Parse(
            System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
        return document.RootElement.GetProperty("sub").GetGuid();
    }
}