using Bogus;

namespace ECommerce.Services.Catalogs.TestShared;

public static class CatalogsTestData
{
    public const string ProductCode = "catalog-001";
    public const string ProductName = "Starter Basket";
    public const decimal ProductPrice = 42.50m;

    private static readonly Faker<CreateProductRequestData> CreateProductRequestFaker =
        new Faker<CreateProductRequestData>().CustomInstantiator(faker =>
            new CreateProductRequestData(
                $"catalog-{faker.Random.AlphaNumeric(10).ToLowerInvariant()}",
                $"{faker.Commerce.ProductAdjective()} {faker.Commerce.ProductMaterial()} {faker.Commerce.ProductName()}",
                decimal.Round(faker.Random.Decimal(5, 200), 2)
            )
        );

    public static CreateProductRequestData NewProductRequest()
    {
        return CreateProductRequestFaker.Generate();
    }
}

public sealed record CreateProductRequestData(string Code, string Name, decimal Price);
