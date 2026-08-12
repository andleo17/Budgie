using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Budgie.IntegrationTests;

/// <summary>
/// Shared helpers for integration tests: registers a fresh user, logs in,
/// configures settings, and creates a default account + person.
/// </summary>
public static class AuthHelper
{
    public static async Task<string> RegisterAndLoginAsync(HttpClient client, string currency = "USD")
    {
        var email = $"auth_{Guid.NewGuid()}@example.com";
        var password = "Password123!";

        await client.PostAsJsonAsync("/api/users/register", new { Email = email, Password = password, Name = "Test User" });
        var login = await client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var result = await login.Content.ReadFromJsonAsync<LoginResult>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result!.Token);
        await client.PostAsJsonAsync("/api/users/settings/setup",
            new { MainCurrency = currency, DefaultCreditConsumptionMode = 1 });

        return result.Token;
    }

    public static async Task<Guid> CreateAccountAsync(HttpClient client, string name, string currency, decimal initialBalance)
    {
        var response = await client.PostAsJsonAsync("/api/finances/accounts",
            new { Name = name, Currency = currency, InitialBalance = initialBalance, Type = 1 });
        var result = await response.Content.ReadFromJsonAsync<IdResult>();
        return result!.Id;
    }

    public static async Task<Guid> CreatePersonAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/relationships/persons", new { Name = name });
        var result = await response.Content.ReadFromJsonAsync<IdResult>();
        return result!.Id;
    }

    public static async Task<Guid> CreateExpenseWithDebtAsync(HttpClient client, Guid accountId, Guid personId,
        decimal amount, decimal contributed, decimal remaining, string currency = "USD")
    {
        var response = await client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = accountId,
            Amount = amount,
            Currency = currency,
            ExchangeRate = 1m,
            Date = DateTime.UtcNow,
            Type = 1,
            Notes = "Gasto",
            Participants = new[]
            {
                new { PersonId = personId, ContributedAmount = contributed, RemainingAmount = remaining, IsTrackingOnly = false, DestinationAccountId = (Guid?)null }
            }
        });
        var result = await response.Content.ReadFromJsonAsync<IdResult>();
        return result!.Id;
    }

    public static async Task<Guid> CreateExpenseWithCashParticipantAsync(HttpClient client, Guid accountId, Guid personId,
        decimal amount, decimal contributed, Guid destinationAccountId, string currency = "USD")
    {
        var response = await client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = accountId,
            Amount = amount,
            Currency = currency,
            ExchangeRate = 1m,
            Date = DateTime.UtcNow,
            Type = 1,
            Notes = "Gasto contado",
            Participants = new[]
            {
                new { PersonId = personId, ContributedAmount = contributed, RemainingAmount = 0m, IsTrackingOnly = false, DestinationAccountId = destinationAccountId }
            }
        });
        var result = await response.Content.ReadFromJsonAsync<IdResult>();
        return result!.Id;
    }

    private record LoginResult(string Token, string RefreshToken);
    private record IdResult(Guid Id);
}
