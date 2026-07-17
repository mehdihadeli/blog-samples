using ECommerce.Services.Catalogs.Products.Features.ProjectingProductReadModel.v1;
using ECommerce.Services.Catalogs.Shared.Contracts;
using ECommerce.Services.Catalogs.Shared.ReadModels;
using MassTransit;
using Moq;

namespace ECommerce.Services.Catalogs.UnitTests;

public class ProjectProductReadModelConsumerTests
{
    [Fact]
    public async Task Consume_ShouldUpsertReadModel()
    {
        var repository = new Mock<IProductReadRepository>();
        var consumer = new ProjectProductReadModelConsumer(repository.Object);
        var command =
            new ECommerce.Services.Shared.Contracts.InternalCommands.ProjectProductReadModel(
                Tests.Shared.SampleData.ProductId,
                "catalog-001",
                "Starter Basket",
                42.50m,
                Tests.Shared.SampleData.CreatedAtUtc
            );

        var context = Mock.Of<
            ConsumeContext<ECommerce.Services.Shared.Contracts.InternalCommands.ProjectProductReadModel>
        >(x => x.Message == command && x.CancellationToken == CancellationToken.None);

        await consumer.Consume(context);

        repository.Verify(
            x =>
                x.UpsertAsync(
                    It.Is<ProductReadModel>(m =>
                        m.Id == command.ProductId
                        && m.Code == command.Code
                        && m.Name == command.Name
                        && m.Price == command.Price
                    ),
                    CancellationToken.None
                ),
            Times.Once
        );
    }
}
