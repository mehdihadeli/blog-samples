using System.Data;
using System.Net;
using System.Net.Http.Json;
using ECommerce.Services.Catalogs.Shared.ReadModels;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Xunit;

namespace ECommerce.Services.Catalogs.IntegrationTests.Products.Features.CreatingProduct.v1;

public class CreateProductTests(CatalogsSharedFixture sharedFixture)
    : CatalogsIntegrationTestBase(sharedFixture)
{
    [Fact]
    public async Task PostProduct_ShouldCreateWriteAndReadModels_AndPublishEvent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Arrange
        using var client = Factory.CreateClient();
        var request = new
        {
            code = "catalog-101",
            name = "Test Basket",
            price = 15.25m,
        };

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/catalogs/products",
            request,
            cancellationToken
        );

        // Assert – HTTP
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateProductResult>(
            cancellationToken
        );
        Assert.NotNull(created);

        // Assert – write model (EF Core)
        await ExecuteCatalogsDbContextAsync(async dbContext =>
        {
            var entity = await dbContext.Products.FindAsync(created!.Id);
            Assert.NotNull(entity);
            Assert.Equal(request.code, entity!.Code);
            Assert.Equal(request.name, entity!.Name);
            Assert.Equal(request.price, entity!.Price);
        });

        // Assert – bus outbox drained, meaning MassTransit delivered the published event.
        var published = await WaitForOutboxToDrainAsync(
            TimeSpan.FromSeconds(30),
            cancellationToken
        );

        Assert.True(
            published,
            "Expected the product-created event to be delivered by the bus outbox."
        );

        // Assert – read model projected to MongoDB
        var mongoClient = new MongoClient(MongoConnectionString);
        var database = mongoClient.GetDatabase("catalogs-mongo");
        var collection = database.GetCollection<ProductReadModel>("product-read-models");

        ProductReadModel? readModel = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var readModels = await collection
                .Find(Builders<ProductReadModel>.Filter.Empty)
                .ToListAsync(cancellationToken);
            readModel = readModels.SingleOrDefault(x => x.Id == created!.Id);
            if (readModel is not null)
            {
                break;
            }

            await Task.Delay(250, cancellationToken);
        }

        Assert.NotNull(readModel);
        Assert.Equal(request.code, readModel!.Code);
        Assert.Equal(request.name, readModel!.Name);
        Assert.Equal(request.price, readModel!.Price);
    }

    private sealed record CreateProductResult(Guid Id, string Code, string Name, decimal Price);

    private async Task<bool> WaitForOutboxToDrainAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            var pendingMessages = await ExecuteCatalogsDbContextAsync(async dbContext =>
            {
                var connection = dbContext.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM \"OutboxMessage\";";

                var result = await command.ExecuteScalarAsync(cancellationToken);
                return Convert.ToInt32(result);
            });

            if (pendingMessages == 0)
            {
                return true;
            }

            await Task.Delay(500, cancellationToken);
        }

        return false;
    }
}
