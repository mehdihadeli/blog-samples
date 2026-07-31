using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Core.Messages;
using ECommerce.Services.Catalogs.TestShared;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
using ECommerce.Services.Shared.Contracts.InternalCommands;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Services.Catalogs.IntegrationTests.Products.Features.CreatingProduct.v1;

public class CreateProductTests(CatalogsSharedFixture sharedFixture)
    : CatalogsIntegrationTestBase(sharedFixture)
{
    [Fact]
    public async Task PostProduct_ShouldCreateWriteAndReadModels_AndPublishEvent()
    {
        var request = CatalogsTestData.NewProductRequest();

        await AssertCreateProductAsync(request);
    }

    [Fact]
    public async Task PostProduct_ShouldPersistOutgoingProductCreatedMessage()
    {
        var request = CatalogsTestData.NewProductRequest();

        await SharedFixture.ShouldPublishing<MessageEnvelope<ProductCreatedV1>>(
            async () =>
            {
                var response = await SharedFixture.GuestClient.PostAsJsonAsync(
                    "/api/v1/catalogs/products",
                    request
                );

                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            },
            TestCancellationToken,
            ignoreMessageTypes: IgnoreScheduledInternalCommands
        );
    }

    [Fact]
    public async Task PostProduct_ShouldProcessOutboxMessage()
    {
        var request = CatalogsTestData.NewProductRequest();

        // Act + Assert — TrackActivity including external transports: after the write
        // transaction commits, the transactional outbox flushes MessageEnvelope<ProductCreatedV1>
        // and hands it to the broker (Sent). No fault published. This is the publisher-side
        // proof that the message went through the outbox during publishing.
        await SharedFixture.ShouldProcessingOutboxMessage<MessageEnvelope<ProductCreatedV1>>(
            async () =>
            {
                var response = await SharedFixture.GuestClient.PostAsJsonAsync(
                    "/api/v1/catalogs/products",
                    request
                );

                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            },
            ignoreMessageTypes: IgnoreScheduledInternalCommands,
            cancellationToken: TestCancellationToken
        );
    }

    [Fact]
    public async Task PostProduct_ShouldProcessInternalCommand()
    {
        var request = CatalogsTestData.NewProductRequest();

        CreateProductResult? created = null;

        // Act + Assert — TrackActivity: after publishing, the internal command
        // ProjectProductReadModel is processed successfully (MessageSucceeded) and its
        // side-effect (Mongo read model upsert) is visible through the API.
        await SharedFixture.ShouldProcessingInternalCommand<ProjectProductReadModel>(
            async () =>
            {
                var response = await SharedFixture.GuestClient.PostAsJsonAsync(
                    "/api/v1/catalogs/products",
                    request
                );

                Assert.Equal(HttpStatusCode.Created, response.StatusCode);

                created = await response.Content.ReadFromJsonAsync<CreateProductResult>();
                Assert.NotNull(created);
            },
            async () =>
            {
                ProductReadModelResult? readModel = null;
                await SharedFixture.WaitUntilConditionMet(
                    async () =>
                    {
                        var readModelResponse = await SharedFixture.GuestClient.GetAsync(
                            $"/api/v1/catalogs/products/read-model/{created!.Id}"
                        );

                        if (readModelResponse.StatusCode != HttpStatusCode.OK)
                            return false;

                        readModel =
                            await readModelResponse.Content.ReadFromJsonAsync<ProductReadModelResult>();
                        return readModel is not null;
                    },
                    cancellationToken: TestCancellationToken
                );

                Assert.NotNull(readModel);
                Assert.Equal(created!.Id, readModel!.Id);
                Assert.Equal(request.Code, readModel.Code);
                Assert.Equal(request.Name, readModel.Name);
                Assert.Equal(request.Price, readModel.Price);
            },
            ignoreMessageTypes: t =>
                typeof(IInternalCommand).IsAssignableFrom(t)
                && t != typeof(ProjectProductReadModel),
            cancellationToken: TestCancellationToken
        );
    }

    /// <summary>
    /// The CreateProduct handler schedules <c>SyncProductToExternalSystem</c> five minutes into
    /// the future. A tracked session waits for ALL tracked activity, so that job would hold the
    /// session open until it times out. These tests assert on the outbox event / internal
    /// command, never on that future background job — skip it.
    /// </summary>
    private static bool IgnoreScheduledInternalCommands(Type messageType) =>
        typeof(IInternalCommand).IsAssignableFrom(messageType);

    private async Task AssertCreateProductAsync(CreateProductRequestData request)
    {
        CreateProductResult? created = null;

        await SharedFixture.ShouldPublishing<MessageEnvelope<ProductCreatedV1>>(
            async () =>
            {
                var response = await SharedFixture.GuestClient.PostAsJsonAsync(
                    "/api/v1/catalogs/products",
                    request
                );

                Assert.Equal(HttpStatusCode.Created, response.StatusCode);

                created = await response.Content.ReadFromJsonAsync<CreateProductResult>();
                Assert.NotNull(created);
            },
            cancellationToken: TestCancellationToken
        );

        await ExecuteCatalogsDbContextAsync(async dbContext =>
        {
            var entity = await dbContext.Products.SingleAsync(x => x.Id == created!.Id);
            Assert.Equal(request.Code, entity.Code);
            Assert.Equal(request.Name, entity.Name);
            Assert.Equal(request.Price, entity.Price);
        });

        ProductReadModelResult? readModel = null;
        await SharedFixture.WaitUntilConditionMet(
            async () =>
            {
                var readModelResponse = await SharedFixture.GuestClient.GetAsync(
                    $"/api/v1/catalogs/products/read-model/{created!.Id}"
                );

                if (readModelResponse.StatusCode != HttpStatusCode.OK)
                    return false;

                readModel =
                    await readModelResponse.Content.ReadFromJsonAsync<ProductReadModelResult>();
                return readModel is not null;
            },
            cancellationToken: TestCancellationToken
        );

        Assert.NotNull(readModel);
        Assert.Equal(created!.Id, readModel!.Id);
        Assert.Equal(request.Code, readModel.Code);
        Assert.Equal(request.Name, readModel.Name);
        Assert.Equal(request.Price, readModel.Price);
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
