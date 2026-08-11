using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class RelationshipsEndpointsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RelationshipsEndpointsTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
        AuthHelper.RegisterAndLoginAsync(_client).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task CreatePerson_ShouldReturn201()
    {
        var response = await _client.PostAsJsonAsync("/api/relationships/persons", new { Name = "Friend" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetPersons_ShouldReturnList()
    {
        await _client.PostAsJsonAsync("/api/relationships/persons", new { Name = "Alice" });

        var response = await _client.GetAsync("/api/relationships/persons");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var persons = await response.Content.ReadFromJsonAsync<List<PersonDto>>();
        persons!.Should().Contain(p => p.Name == "Alice");
    }

    [Fact]
    public async Task UpdatePerson_ShouldReturn204()
    {
        var create = await _client.PostAsJsonAsync("/api/relationships/persons", new { Name = "Before" });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var update = await _client.PutAsJsonAsync($"/api/relationships/persons/{created!.Id}",
            new { Name = "After", TagNames = (string[]?)null });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeletePerson_ShouldReturn204()
    {
        var create = await _client.PostAsJsonAsync("/api/relationships/persons", new { Name = "DeleteMe" });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var delete = await _client.DeleteAsync($"/api/relationships/persons/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CreateAndGetTags_ShouldWork()
    {
        await _client.PostAsJsonAsync("/api/relationships/tags", new { Name = "Work", Color = "#ff0000" });

        var response = await _client.GetAsync("/api/relationships/tags");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tags = await response.Content.ReadFromJsonAsync<List<TagDto>>();
        tags!.Should().Contain(t => t.Name == "Work");
    }

    private record IdResponse(Guid Id);
    private record PersonDto(Guid Id, string Name);
    private record TagDto(Guid Id, string Name, string? Color);
}
