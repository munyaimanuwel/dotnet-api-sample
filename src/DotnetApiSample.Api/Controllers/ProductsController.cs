using DotnetApiSample.Application.Abstractions;
using DotnetApiSample.Application.Messaging;
using DotnetApiSample.Application.Products;
using DotnetApiSample.Domain;
using Microsoft.AspNetCore.Mvc;

namespace DotnetApiSample.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductRepository _repository;
    private readonly IEventPublisher _eventPublisher;

    public ProductsController(IProductRepository repository, IEventPublisher eventPublisher)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Product>>> GetAll(CancellationToken cancellationToken)
    {
        var products = await _repository.GetAllAsync(cancellationToken);

        return Ok(products);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetById(int id, CancellationToken cancellationToken)
    {
        var product = await _repository.GetByIdAsync(id, cancellationToken);

        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Name = request.Name,
            Sku = request.Sku,
            Price = request.Price
        };

        var created = await _repository.AddAsync(product, cancellationToken);

        // No outbox in this sample: the row is committed before the event is published, so a crash
        // in between drops the event. Called out in the README.
        await _eventPublisher.PublishAsync(
            new ProductCreatedEvent(
                Guid.NewGuid(),
                created.Id,
                created.Sku,
                created.Name,
                created.Price,
                DateTimeOffset.UtcNow),
            MessageRoutingKeys.ProductCreated,
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Id = id,
            Name = request.Name,
            Sku = request.Sku,
            Price = request.Price
        };

        var updated = await _repository.UpdateAsync(product, cancellationToken);

        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}
