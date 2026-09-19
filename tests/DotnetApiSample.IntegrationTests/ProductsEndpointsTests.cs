using System.Net;
using System.Net.Http.Json;
using DotnetApiSample.Application.Products;
using DotnetApiSample.Domain;

namespace DotnetApiSample.IntegrationTests;

public class ProductsEndpointsTests : IClassFixture<ProductsApiFactory>
{
    private readonly ProductsApiFactory _factory;

    public ProductsEndpointsTests(ProductsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_ReturnsSeededProducts()
    {
        await _factory.ResetAsync();
        var client = _factory.CreateClient();

        var products = await client.GetFromJsonAsync<List<Product>>("/api/products");

        Assert.NotNull(products);
        Assert.Equal(3, products.Count);
        Assert.Contains(products, product => product.Sku == "ESP-001");
    }

    [Fact]
    public async Task Create_ThenGetById_ReturnsCreatedProduct()
    {
        await _factory.ResetAsync();
        var client = _factory.CreateClient();
        var request = new CreateProductRequest { Name = "Pour-over Set", Sku = "POV-004", Price = 32.00m };

        var response = await client.PostAsJsonAsync("/api/products", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Product>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.NotNull(response.Headers.Location);

        var fetched = await client.GetFromJsonAsync<Product>($"/api/products/{created.Id}");
        Assert.NotNull(fetched);
        Assert.Equal("POV-004", fetched.Sku);
        Assert.Equal(32.00m, fetched.Price);
    }

    [Fact]
    public async Task Update_ExistingProduct_ReturnsNoContentAndPersistsChange()
    {
        await _factory.ResetAsync();
        var client = _factory.CreateClient();
        var request = new UpdateProductRequest { Name = "Renamed", Sku = "ESP-001", Price = 199.00m };

        var response = await client.PutAsJsonAsync("/api/products/1", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var fetched = await client.GetFromJsonAsync<Product>("/api/products/1");
        Assert.NotNull(fetched);
        Assert.Equal("Renamed", fetched.Name);
        Assert.Equal(199.00m, fetched.Price);
    }

    [Fact]
    public async Task Delete_ExistingProduct_ReturnsNoContentThenNotFound()
    {
        await _factory.ResetAsync();
        var client = _factory.CreateClient();

        var deleteResponse = await client.DeleteAsync("/api/products/1");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/products/1");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetById_MissingProduct_ReturnsNotFound()
    {
        await _factory.ResetAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
