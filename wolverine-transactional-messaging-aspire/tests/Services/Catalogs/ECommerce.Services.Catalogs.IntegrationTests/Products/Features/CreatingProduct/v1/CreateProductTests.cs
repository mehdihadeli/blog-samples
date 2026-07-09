using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tests.Shared.Factory;
using Tests.Shared.Fixtures;

namespace ECommerce.Services.Catalogs.IntegrationTests.Products.Features.CreatingProduct.v1;

public class CreateProductTests(
    PostgresContainerFixture postgres,
    RabbitMqContainerFixture rabbitMq,
    KafkaContainerFixture kafka,
    MongoContainerFixture mongo
) : CatalogsIntegrationTestBase(postgres, rabbitMq, kafka, mongo)
{
    [Fact]
    public async Task PostProduct_ShouldCreateWriteAndReadModels()
    {
        using var appFactory = new CustomWebApplicationFactory<Program>()
            .WithSetting("Messaging:Transport", "rabbitmq")
            .WithSetting("ConnectionStrings:catalogsdb", Postgres.ConnectionString)
            .WithSetting("ConnectionStrings:catalogs-mongo", Mongo.ConnectionString)
            .WithSetting("ConnectionStrings:rabbitmq", RabbitMq.ConnectionString);

        await AssertCreateProductAsync(appFactory, "catalog-101", "Test Basket", 15.25m);
    }

    [Fact]
    public async Task PostProduct_ShouldCreateWriteAndReadModels_WithKafkaTransport()
    {
        await Kafka.EnsureStartedAsync();
        await Kafka.CleanupTopicsAsync();

        using var appFactory = new CustomWebApplicationFactory<Program>()
            .WithSetting("Messaging:Transport", "kafka")
            .WithSetting("ConnectionStrings:catalogsdb", Postgres.ConnectionString)
            .WithSetting("ConnectionStrings:catalogs-mongo", Mongo.ConnectionString)
            .WithSetting("ConnectionStrings:kafka", Kafka.BootstrapServers);

        await AssertCreateProductAsync(
            appFactory,
            "catalog-kafka-101",
            "Kafka Test Basket",
            18.75m
        );
    }

    private async Task AssertCreateProductAsync(
        CustomWebApplicationFactory<Program> appFactory,
        string code,
        string name,
        decimal price
    )
    {
        await ExecuteCatalogsDbContextAsync(_ => Task.CompletedTask);

        using var client = appFactory.CreateClient();
        var request = new
        {
            code,
            name,
            price,
        };

        var response = await client.PostAsJsonAsync("/api/v1/catalogs/products", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateProductResult>();
        Assert.NotNull(created);

        await ExecuteCatalogsDbContextAsync(async dbContext =>
        {
            var entity = await dbContext.Products.SingleAsync(x => x.Id == created!.Id);
            Assert.Equal(code, entity.Code);
            Assert.Equal(name, entity.Name);
            Assert.Equal(price, entity.Price);
        });

        ProductReadModelResult? readModel = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var readModelResponse = await client.GetAsync(
                $"/api/v1/catalogs/products/read-model/{created!.Id}"
            );

            if (readModelResponse.StatusCode == HttpStatusCode.OK)
            {
                readModel =
                    await readModelResponse.Content.ReadFromJsonAsync<ProductReadModelResult>();
                break;
            }

            await Task.Delay(250);
        }

        Assert.NotNull(readModel);
        Assert.Equal(created!.Id, readModel!.Id);
        Assert.Equal(code, readModel.Code);
        Assert.Equal(name, readModel.Name);
        Assert.Equal(price, readModel.Price);
    }

    private sealed record CreateProductResult(Guid Id, string Code, string Name, decimal Price);

    private sealed record ProductReadModelResult(
        Guid Id,
        string Code,
        string Name,
        decimal Price,
        DateTime CreatedAtUtc,
        DateTime ProjectedAtUtc
    );
}
