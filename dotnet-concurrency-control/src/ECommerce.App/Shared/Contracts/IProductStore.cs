using ECommerce.Products.Models;

namespace ECommerce.Shared.Contracts;

// Abstraction over the product inventory store.
// Interfaces live in Shared/Contracts/ — same pattern as Catalogs.
public interface IProductStore
{
    Product Get(Guid id);
    IReadOnlyList<Product> GetAll();
    bool Exists(Guid id);
    (bool success, Product? product, int storeVersion) TryUpdate(
        Guid id,
        int expectedVersion,
        Func<Product, Product> transform
    );
    void Write(Product product);
    void Seed(Product product);
}
