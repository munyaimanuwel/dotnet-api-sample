using DotnetApiSample.Application.Abstractions;
using DotnetApiSample.Domain;

namespace DotnetApiSample.UnitTests.Fakes;

internal sealed class FakeProductRepository : IProductRepository
{
    private readonly List<Product> _products = [];

    public Product? LastAdded { get; private set; }

    public void Seed(params Product[] products) => _products.AddRange(products);

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Product>>(_products);

    public Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken)
        => Task.FromResult(_products.SingleOrDefault(product => product.Id == id));

    public Task<Product> AddAsync(Product product, CancellationToken cancellationToken)
    {
        product.Id = _products.Count + 1;
        _products.Add(product);
        LastAdded = product;

        return Task.FromResult(product);
    }

    public Task<bool> UpdateAsync(Product product, CancellationToken cancellationToken)
    {
        var existing = _products.SingleOrDefault(candidate => candidate.Id == product.Id);
        if (existing is null)
        {
            return Task.FromResult(false);
        }

        existing.Name = product.Name;
        existing.Sku = product.Sku;
        existing.Price = product.Price;

        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
        => Task.FromResult(_products.RemoveAll(product => product.Id == id) > 0);
}
