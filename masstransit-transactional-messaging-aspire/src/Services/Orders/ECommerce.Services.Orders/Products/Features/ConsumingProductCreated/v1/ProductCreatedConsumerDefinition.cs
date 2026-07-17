using ECommerce.Services.Orders.Shared.Data;
using MassTransit;

namespace ECommerce.Services.Orders.Products.Features.ConsumingProductCreated.v1;

public sealed class ProductCreatedConsumerDefinition : ConsumerDefinition<ProductCreatedConsumer>
{
    public ProductCreatedConsumerDefinition()
    {
        EndpointName = "orders-products";
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<ProductCreatedConsumer> consumerConfigurator,
        IRegistrationContext context
    )
    {
        // Retry, delayed redelivery, and consumer outbox are applied centrally
        // via MassTransitServiceCollectionExtensions.ApplyEndpointPolicies.
    }
}
