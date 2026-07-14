using System.Net;
using System.Net.Http.Json;
using ECommerce.Services.Catalogs.TestShared;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Tests.Shared.Factory;

namespace ECommerce.Services.Catalogs.IntegrationTests.Products.Features.CreatingProduct.v1;

public class CreateProductTests(CatalogsSharedFixture sharedFixture)
    : CatalogsIntegrationTestBase(sharedFixture)
{
    [Fact]
    public async Task PostProduct_ShouldCreateWriteAndReadModels_AndPublishEvent_ForRabbitMq()
    {
        var request = CatalogsTestData.NewProductRequest();

        await AssertCreateProductAsync(Factory, request);
    }

    [Fact]
    public async Task PostProduct_ShouldCreateWriteAndReadModelsAndPublishEvent_ForKafka()
    {
        var request = CatalogsTestData.NewProductRequest();

        await Kafka.EnsureStartedAsync();
        using var appFactory = SharedFixture.CreateFactory("kafka");

        await AssertCreateProductAsync(appFactory, request);
    }

    [Fact]
    public async Task PostProduct_ShouldPersistOutgoingProductCreatedMessage_ForRabbitMq()
    {
        await AssertOutgoingProductCreatedMessageAsync(Factory);
    }

    [Fact]
    public async Task PostProduct_ShouldPersistOutgoingProductCreatedMessage_ForKafka()
    {
        await Kafka.EnsureStartedAsync();
        using var appFactory = SharedFixture.CreateFactory("kafka");

        await AssertOutgoingProductCreatedMessageAsync(appFactory);
    }

    private async Task AssertCreateProductAsync(
        CustomWebApplicationFactory<Program> appFactory,
        CreateProductRequestData request
    )
    {
        await SharedFixture.ExecuteCatalogsDbContextAsync(_ => Task.CompletedTask);

        var created = await CreateProductAsync(appFactory, request);

        await ShouldPublish<ProductCreatedV1>();

        await SharedFixture.ExecuteCatalogsDbContextAsync(async dbContext =>
        {
            var entity = await dbContext.Products.SingleAsync(x => x.Id == created.Id);
            Assert.Equal(request.Code, entity.Code);
            Assert.Equal(request.Name, entity.Name);
            Assert.Equal(request.Price, entity.Price);
        });

        using var client = appFactory.CreateClient();
        ProductReadModelResult? readModel = null;
        await SharedFixture.WaitUntilConditionMet(async () =>
        {
            var readModelResponse = await client.GetAsync(
                $"/api/v1/catalogs/products/read-model/{created.Id}"
            );

            if (readModelResponse.StatusCode != HttpStatusCode.OK)
            {
                return false;
            }

            readModel = await readModelResponse.Content.ReadFromJsonAsync<ProductReadModelResult>();
            return readModel is not null;
        });

        Assert.NotNull(readModel);
        Assert.Equal(created.Id, readModel!.Id);
        Assert.Equal(request.Code, readModel.Code);
        Assert.Equal(request.Name, readModel.Name);
        Assert.Equal(request.Price, readModel.Price);
    }

    private async Task AssertOutgoingProductCreatedMessageAsync(
        CustomWebApplicationFactory<Program> appFactory
    )
    {
        await SharedFixture.ExecuteCatalogsDbContextAsync(_ => Task.CompletedTask);

        var queuedMessages = await SharedFixture.CountOutgoingEnvelopeRowsAsync(
            $"%{MessagingConstants.ProductCreatedQueue}%"
        );

        Assert.True(queuedMessages > 0);
    }

    private static async Task<CreateProductResult> CreateProductAsync(
        CustomWebApplicationFactory<Program> appFactory,
        CreateProductRequestData request
    )
    {
        using var client = appFactory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/catalogs/products", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateProductResult>();
        Assert.NotNull(created);
        return created!;
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
