using System.Net;
using System.Net.Http.Json;
using Budgie.Modules.Finances.Application.Categories;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class CategoriesEndpointsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;
    private string _token = string.Empty;

    public CategoriesEndpointsTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
    }

    private async Task AuthenticateAsync()
    {
        if (!string.IsNullOrEmpty(_token)) return;

        var email = $"categories_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register", new { Email = email, Password = "Password123!", Name = "Test" });
        var login = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = "Password123!" });
        var result = await login.Content.ReadFromJsonAsync<LoginResult>();
        _token = result!.Token;
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        await _client.PostAsJsonAsync("/api/users/settings/setup", new { MainCurrency = "USD", DefaultCreditConsumptionMode = 1 });
    }

    [Fact]
    public async Task CreateCategory_ShouldReturn201()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/api/finances/categories",
            new { Name = "Groceries", Color = "#00FF00", Icon = "cart" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<IdResponse>();
        result!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateCategory_WithParent_ShouldReturn201()
    {
        await AuthenticateAsync();

        var parent = await _client.PostAsJsonAsync("/api/finances/categories",
            new { Name = "Food", Color = "#FF0000", Icon = "pizza" });
        var parentResult = await parent.Content.ReadFromJsonAsync<IdResponse>();

        var child = await _client.PostAsJsonAsync("/api/finances/categories",
            new { Name = "Pizza", Color = "#FFAA00", Icon = "slice", ParentId = parentResult!.Id });
        child.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetCategories_ShouldReturnList()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync("/api/finances/categories");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>();
        categories.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCategoryById_ShouldReturnCategory()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/finances/categories",
            new { Name = "SpecificCat", Color = "#123456", Icon = "tag" });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var response = await _client.GetAsync($"/api/finances/categories/{created!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var category = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        category!.Name.Should().Be("SpecificCat");
    }

    [Fact]
    public async Task GetCategoryById_NotFound_ShouldReturn404()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync($"/api/finances/categories/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateCategory_ShouldReturn204()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/finances/categories",
            new { Name = "OldCat", Color = "#000000", Icon = "dot" });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var update = await _client.PutAsJsonAsync($"/api/finances/categories/{created!.Id}",
            new { Name = "NewCat", Color = "#FFFFFF", Icon = "circle", Type = 0, ParentId = (Guid?)null });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await _client.GetAsync($"/api/finances/categories/{created.Id}");
        var category = await get.Content.ReadFromJsonAsync<CategoryResponse>();
        category!.Name.Should().Be("NewCat");
    }

    [Fact]
    public async Task UpdateCategory_NotFound_ShouldReturn400()
    {
        await AuthenticateAsync();

        var response = await _client.PutAsJsonAsync($"/api/finances/categories/{Guid.NewGuid()}",
            new { Name = "Ghost", Color = "#000000", Icon = "x", Type = 0, ParentId = (Guid?)null });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteCategory_ShouldReturn204()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/finances/categories",
            new { Name = "DeleteMe", Color = "#FF0000", Icon = "trash" });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var delete = await _client.DeleteAsync($"/api/finances/categories/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteCategory_NotFound_ShouldReturn400()
    {
        await AuthenticateAsync();

        var response = await _client.DeleteAsync($"/api/finances/categories/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private record LoginResult(string Token);
}
