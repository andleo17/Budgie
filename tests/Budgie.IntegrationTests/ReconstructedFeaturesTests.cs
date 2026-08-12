using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class ReconstructedFeaturesTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Guid _accountId;
    private readonly Guid _personId;

    public ReconstructedFeaturesTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
        AuthHelper.RegisterAndLoginAsync(_client).GetAwaiter().GetResult();
        _accountId = AuthHelper.CreateAccountAsync(_client, "FeatureAcc", "USD", 1000m).GetAwaiter().GetResult();
        _personId = AuthHelper.CreatePersonAsync(_client, "Ana").GetAwaiter().GetResult();
    }

    [Fact]
    public async Task UpdateAccount_ShouldReturn204()
    {
        var response = await _client.PutAsJsonAsync($"/api/finances/accounts/{_accountId}",
            new { Name = "Renamed" });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var account = await _client.GetFromJsonAsync<AccountDto>($"/api/finances/accounts/{_accountId}");
        account!.Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task DeleteAccount_ShouldReturn204()
    {
        var create = await _client.PostAsJsonAsync("/api/finances/accounts",
            new { Name = "Temp", Currency = "USD", InitialBalance = 0m, Type = 1 });
        var created = await create.Content.ReadFromJsonAsync<IdDto>();

        var response = await _client.DeleteAsync($"/api/finances/accounts/{created!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CreateAccount_WithInitialBalance_CreatesTransaction()
    {
        var create = await _client.PostAsJsonAsync("/api/finances/accounts",
            new { Name = "Init", Currency = "USD", InitialBalance = 500m, Type = 1 });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<IdDto>();

        var account = await _client.GetFromJsonAsync<AccountDto>($"/api/finances/accounts/{created!.Id}");
        account!.Balance.Should().Be(500m);

        var tx = await _client.GetFromJsonAsync<PagedDto>($"/api/finances/transactions?accountIds={created.Id}");
        tx!.Items.Should().Contain(i => i.isInitialBalance);
    }

    [Fact]
    public async Task UpdateTransaction_ShouldReturn204()
    {
        var create = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = _accountId, Amount = 50m, Currency = "USD", ExchangeRate = 1m,
            Date = DateTime.UtcNow, Type = 1, Notes = "Antes"
        });
        var created = await create.Content.ReadFromJsonAsync<IdDto>();

        var response = await _client.PutAsJsonAsync($"/api/finances/transactions/{created!.Id}", new
        {
            CategoryId = (Guid?)null, Amount = 80m, Currency = "USD", ExchangeRate = 1m,
            Date = DateTime.UtcNow, Notes = "Despues", Type = 1, Installments = 1
        });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var account = await _client.GetFromJsonAsync<AccountDto>($"/api/finances/accounts/{_accountId}");
        account!.Balance.Should().Be(1000m - 80m);
    }

    [Fact]
    public async Task Transfer_ShouldMoveMoney()
    {
        var destId = AuthHelper.CreateAccountAsync(_client, "Dest", "USD", 100m).GetAwaiter().GetResult();

        var response = await _client.PostAsJsonAsync("/api/finances/transactions/transfer", new
        {
            FromAccountId = _accountId, ToAccountId = destId, Amount = 200m, Fee = 0m,
            Currency = "USD", ExchangeRate = 1m, Date = DateTime.UtcNow, Notes = "Traspaso"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var source = await _client.GetFromJsonAsync<AccountDto>($"/api/finances/accounts/{_accountId}");
        var dest = await _client.GetFromJsonAsync<AccountDto>($"/api/finances/accounts/{destId}");
        source!.Balance.Should().Be(1000m - 200m);
        dest!.Balance.Should().Be(100m + 200m);
    }

    [Fact]
    public async Task CategoryCrud_ShouldWork()
    {
        var create = await _client.PostAsJsonAsync("/api/finances/categories",
            new { Name = "Comida", Type = 1, Color = "#ff0000" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<IdDto>();

        var update = await _client.PutAsJsonAsync($"/api/finances/categories/{created!.Id}",
            new { Name = "Alimentos", Color = "#00ff00", Icon = "utensils", Type = 1 });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var categories = await _client.GetFromJsonAsync<List<CategoryDto>>("/api/finances/categories");
        categories.Should().Contain(c => c.Name == "Alimentos");

        var del = await _client.DeleteAsync($"/api/finances/categories/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TagUpdateDelete_ShouldWork()
    {
        var create = await _client.PostAsJsonAsync("/api/relationships/tags", new { Name = "Tag1", Color = "#000000" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<IdDto>();

        var update = await _client.PutAsJsonAsync($"/api/relationships/tags/{created!.Id}", new { Name = "TagRenamed", Color = "#ffffff" });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var tags = await _client.GetFromJsonAsync<List<TagDto>>("/api/relationships/tags");
        tags.Should().Contain(t => t.Name == "TagRenamed");

        var del = await _client.DeleteAsync($"/api/relationships/tags/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GoalCrud_ShouldWork()
    {
        var create = await _client.PostAsJsonAsync("/api/finances/goals",
            new { AccountId = _accountId, TargetAmount = 5000m, TargetDate = (DateTime?)null });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<IdDto>();

        var goals = await _client.GetFromJsonAsync<List<GoalDto>>("/api/finances/goals");
        goals!.Should().Contain(g => g.Id == created!.Id);

        var update = await _client.PutAsJsonAsync($"/api/finances/goals/{created.Id}",
            new { TargetAmount = 8000m, TargetDate = (DateTime?)null });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var del = await _client.DeleteAsync($"/api/finances/goals/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RecurringTemplateCrud_ShouldWork()
    {
        var create = await _client.PostAsJsonAsync("/api/finances/transactions/templates", new
        {
            Name = "Sueldo", AccountId = _accountId, CategoryId = (Guid?)null,
            Amount = 3000m, Currency = "USD", ExchangeRate = 1m, Type = 0,
            Notes = (string?)null, Installments = 1, IsInterestFree = true,
            Applicability = 0, Frequency = (int?)null, CustomIntervalDays = (int?)null,
            EndType = (int?)null, EndAfterCount = (int?)null, EndDate = (DateTime?)null,
            StartDate = (DateTime?)null,
            Participants = (object[]?)null
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<IdDto>();

        var templates = await _client.GetFromJsonAsync<List<TemplateDto>>("/api/finances/transactions/templates");
        templates!.Should().Contain(t => t.Id == created!.Id);

        var apply = await _client.PostAsync($"/api/finances/transactions/templates/{created.Id}/apply", null);
        apply.StatusCode.Should().Be(HttpStatusCode.OK);

        var del = await _client.DeleteAsync($"/api/finances/transactions/templates/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private record IdDto(Guid Id);
    private record AccountDto(Guid Id, string Name, decimal Balance, string Currency);
    private record PagedDto(List<TransactionItem> Items, int TotalCount);
    private record TransactionItem(Guid Id, bool isInitialBalance);
    private record CategoryDto(Guid Id, string Name);
    private record TagDto(Guid Id, string Name);
    private record GoalDto(Guid Id);
    private record TemplateDto(Guid Id);
}
