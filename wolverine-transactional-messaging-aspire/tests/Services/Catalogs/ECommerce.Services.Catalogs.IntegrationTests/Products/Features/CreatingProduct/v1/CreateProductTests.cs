using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Abstractions.Messages;
using ECommerce.Services.Catalogs.TestShared;
using ECommerce.Services.Shared.Contracts.IntegrationEvents;
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
            TestContext.Current.CancellationToken
        );
    }

    private async Task AssertCreateProductAsync(CreateProductRequestData request)
    {
        CreateProductResult? created = null;

        await SharedFixture.ShouldPublishing<MessageEnvelope<ProductCreatedV1>>(async () =>
        {
            var response = await SharedFixture.GuestClient.PostAsJsonAsync(
                "/api/v1/catalogs/products",
                request
            );

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            created = await response.Content.ReadFromJsonAsync<CreateProductResult>();
            Assert.NotNull(created);
        });

        await ExecuteCatalogsDbContextAsync(async dbContext =>
        {
            var entity = await dbContext.Products.SingleAsync(x => x.Id == created!.Id);
            Assert.Equal(request.Code, entity.Code);
            Assert.Equal(request.Name, entity.Name);
            Assert.Equal(request.Price, entity.Price);
        });

        ProductReadModelResult? readModel = null;
        await SharedFixture.WaitUntilConditionMet(async () =>
        {
            var readModelResponse = await SharedFixture.GuestClient.GetAsync(
                $"/api/v1/catalogs/products/read-model/{created!.Id}"
            );

            if (readModelResponse.StatusCode != HttpStatusCode.OK)
                return false;

            readModel = await readModelResponse.Content.ReadFromJsonAsync<ProductReadModelResult>();
            return readModel is not null;
        });

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
