using DotnetApiSample.Api.Controllers;
using DotnetApiSample.Application.Messaging;
using DotnetApiSample.Application.Products;
using DotnetApiSample.Domain;
using DotnetApiSample.UnitTests.Fakes;
using Microsoft.AspNetCore.Mvc;

namespace DotnetApiSample.UnitTests;

public class ProductsControllerTests
{
    private const int MissingId = 999;

    [Fact]
    public async Task GetAll_ReturnsAllProducts()
    {
        var harness = CreateHarness();

        var result = await harness.Controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var products = Assert.IsAssignableFrom<IReadOnlyList<Product>>(ok.Value);
        Assert.Equal(2, products.Count);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsProduct()
    {
        var harness = CreateHarness();

        var result = await harness.Controller.GetById(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var product = Assert.IsType<Product>(ok.Value);
        Assert.Equal("ESP-001", product.Sku);
    }

    [Fact]
    public async Task GetById_MissingId_ReturnsNotFound()
    {
        var harness = CreateHarness();

        var result = await harness.Controller.GetById(MissingId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction_AndPersistsMappedProduct()
    {
        var harness = CreateHarness();
        var request = new CreateProductRequest { Name = "Pour-over Set", Sku = "POV-004", Price = 32.00m };

        var result = await harness.Controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(ProductsController.GetById), created.ActionName);
        Assert.Equal(3, Assert.IsType<int>(created.RouteValues!["id"]));

        var product = Assert.IsType<Product>(created.Value);
        Assert.Equal("Pour-over Set", product.Name);
        Assert.Equal("POV-004", product.Sku);
        Assert.Equal(32.00m, product.Price);
        Assert.Same(product, harness.Repository.LastAdded);
    }

    [Fact]
    public async Task Create_PublishesProductCreatedEvent()
    {
        var harness = CreateHarness();
        var request = new CreateProductRequest { Name = "Pour-over Set", Sku = "POV-004", Price = 32.00m };

        await harness.Controller.Create(request, CancellationToken.None);

        var (message, routingKey) = Assert.Single(harness.Publisher.Published);
        Assert.Equal(MessageRoutingKeys.ProductCreated, routingKey);

        var @event = Assert.IsType<ProductCreatedEvent>(message);
        Assert.Equal(3, @event.ProductId);
        Assert.Equal("POV-004", @event.Sku);
        Assert.Equal(32.00m, @event.Price);
        Assert.NotEqual(Guid.Empty, @event.EventId);
    }

    [Fact]
    public async Task Update_ExistingId_ReturnsNoContent()
    {
        var harness = CreateHarness();
        var request = new UpdateProductRequest { Name = "Renamed", Sku = "ESP-001", Price = 199.00m };

        var result = await harness.Controller.Update(1, request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(harness.Publisher.Published);
    }

    [Fact]
    public async Task Update_MissingId_ReturnsNotFound()
    {
        var harness = CreateHarness();
        var request = new UpdateProductRequest { Name = "Renamed", Sku = "ESP-001", Price = 199.00m };

        var result = await harness.Controller.Update(MissingId, request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_ExistingId_ReturnsNoContent()
    {
        var harness = CreateHarness();

        var result = await harness.Controller.Delete(1, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_MissingId_ReturnsNotFound()
    {
        var harness = CreateHarness();

        var result = await harness.Controller.Delete(MissingId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private static Harness CreateHarness()
    {
        var repository = new FakeProductRepository();
        repository.Seed(
            new Product { Id = 1, Name = "Espresso Machine", Sku = "ESP-001", Price = 249.00m, CreatedAt = DateTime.UtcNow },
            new Product { Id = 2, Name = "Burr Grinder", Sku = "GRD-002", Price = 89.50m, CreatedAt = DateTime.UtcNow });

        var publisher = new FakeEventPublisher();

        return new Harness(new ProductsController(repository, publisher), repository, publisher);
    }

    private sealed record Harness(
        ProductsController Controller,
        FakeProductRepository Repository,
        FakeEventPublisher Publisher);
}
