using System.Net;
using System.Net.Http.Json;
using Budgie.Modules.Relationships.Application.Persons;
using Budgie.Modules.Relationships.Application.Tags;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class RelationshipsEndpointsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;
    private string _token = string.Empty;

    public RelationshipsEndpointsTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
    }

    private async Task AuthenticateAsync()
    {
        if (!string.IsNullOrEmpty(_token)) return;

        var email = $"rel_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register", new { Email = email, Password = "Password123!", Name = "Test" });
        var login = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = "Password123!" });
        var result = await login.Content.ReadFromJsonAsync<LoginResult>();
        _token = result!.Token;
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        await _client.PostAsJsonAsync("/api/users/settings/setup", new { MainCurrency = "USD", DefaultCreditConsumptionMode = 1 });
    }

    [Fact]
    public async Task CreatePerson_ShouldReturn201()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/api/relationships/persons", new { Name = "Alice" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<IdResponse>();
        result!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetPersons_ShouldReturnList()
    {
        await AuthenticateAsync();

        await _client.PostAsJsonAsync("/api/relationships/persons", new { Name = "Person1" });
        await _client.PostAsJsonAsync("/api/relationships/persons", new { Name = "Person2" });

        var response = await _client.GetAsync("/api/relationships/persons");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var persons = await response.Content.ReadFromJsonAsync<List<PersonResponse>>();
        persons.Should().NotBeNull();
        persons!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetPersonById_ShouldReturnPerson()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/relationships/persons", new { Name = "Bob" });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var response = await _client.GetAsync($"/api/relationships/persons/{created!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var person = await response.Content.ReadFromJsonAsync<PersonResponse>();
        person!.Name.Should().Be("Bob");
    }

    [Fact]
    public async Task GetPersonById_NotFound_ShouldReturn404()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync($"/api/relationships/persons/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdatePerson_ShouldReturn204()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/relationships/persons", new { Name = "OldName" });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var update = await _client.PutAsJsonAsync($"/api/relationships/persons/{created!.Id}",
            new { Name = "NewName" });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdatePerson_WithTags_ShouldReturn204()
    {
        await AuthenticateAsync();

        var tag = await _client.PostAsJsonAsync("/api/relationships/tags", new { Name = "Friend" });
        var tagResult = await tag.Content.ReadFromJsonAsync<TagIdResponse>();
        var create = await _client.PostAsJsonAsync("/api/relationships/persons", new { Name = "Tagged Person" });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var update = await _client.PutAsJsonAsync($"/api/relationships/persons/{created!.Id}",
            new { Name = "Tagged Person", TagNames = new[] { "Friend" } });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeletePerson_ShouldReturn204()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/relationships/persons", new { Name = "DeleteMe" });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var delete = await _client.DeleteAsync($"/api/relationships/persons/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeletePerson_NotFound_ShouldReturn400()
    {
        await AuthenticateAsync();

        var response = await _client.DeleteAsync($"/api/relationships/persons/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTag_ShouldReturn201()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/api/relationships/tags", new { Name = "Important" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<TagIdResponse>();
        result!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTags_ShouldReturnList()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync("/api/relationships/tags");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tags = await response.Content.ReadFromJsonAsync<List<TagResponse>>();
        tags.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateTag_ShouldReturn204()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/relationships/tags", new { Name = "OldTag" });
        var created = await create.Content.ReadFromJsonAsync<TagIdResponse>();

        var update = await _client.PutAsJsonAsync($"/api/relationships/tags/{created!.Id}",
            new { Name = "NewTag" });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteTag_ShouldReturn204()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/relationships/tags", new { Name = "DeleteTag" });
        var created = await create.Content.ReadFromJsonAsync<TagIdResponse>();

        var delete = await _client.DeleteAsync($"/api/relationships/tags/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteTag_NotFound_ShouldReturn400()
    {
        await AuthenticateAsync();

        var response = await _client.DeleteAsync($"/api/relationships/tags/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePerson_WithExistingTag_ShouldReturn201()
    {
        await AuthenticateAsync();

        await _client.PostAsJsonAsync("/api/relationships/tags", new { Name = "Friend", Color = "#00FF00" });

        var response = await _client.PostAsJsonAsync("/api/relationships/persons",
            new { Name = "Tagged Alice", TagNames = new[] { "Friend" } });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatePerson_WithNewTag_ShouldCreateTagAndPerson()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/api/relationships/persons",
            new { Name = "Tagged Bob", TagNames = new[] { "NewTag" } });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var tags = await _client.GetFromJsonAsync<List<TagResponse>>("/api/relationships/tags");
        tags.Should().Contain(t => t.Name == "NewTag");
    }

    [Fact]
    public async Task CreatePerson_WithExistingAndNewTags_ShouldReturn201()
    {
        await AuthenticateAsync();

        await _client.PostAsJsonAsync("/api/relationships/tags", new { Name = "Family" });

        var response = await _client.PostAsJsonAsync("/api/relationships/persons",
            new { Name = "Tagged Charlie", TagNames = new[] { "Family", "Friend" } });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateTag_WithEmptyColor_ShouldReturn201()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/api/relationships/tags",
            new { Name = "EmptyColor", Color = "" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private record LoginResult(string Token);
    private record TagIdResponse(Guid Id);
    private record TagResponse(Guid Id, string Name, string? Color);
}
