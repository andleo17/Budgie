using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class CategoriesEndpointsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CategoriesEndpointsTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
        AuthHelper.RegisterAndLoginAsync(_client).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task GetCategories_ShouldReturnSystemCategories()
    {
        var response = await _client.GetAsync("/api/finances/categories");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();
        categories!.Should().NotBeEmpty();
        categories.Should().Contain(c => c.IsSystem);
    }

    private record CategoryDto(Guid Id, string Name, bool IsSystem);
}
