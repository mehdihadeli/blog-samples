using ECommerce.Services.Catalogs.TestShared;
using Tests.Shared;

namespace ECommerce.Services.Catalogs.UnitTests;

public class MessageEnvelopeTests
{
    [Fact]
    public void Create_ShouldPopulate_Metadata_AndPayload()
    {
        var correlationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var messageId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var envelope = SampleData.ProductCreatedEnvelope(correlationId, messageId);

        Assert.Equal(messageId, envelope.MessageId);
        Assert.Equal(correlationId, envelope.CorrelationId);
        Assert.Equal(SampleData.CreatedAtUtc, envelope.OccurredAtUtc);
        Assert.Equal(SampleData.ProductId, envelope.Message.ProductId);
        Assert.Equal(CatalogsTestData.ProductCode, envelope.Message.Code);
        Assert.Equal(CatalogsTestData.ProductName, envelope.Message.Name);
        Assert.Equal(CatalogsTestData.ProductPrice, envelope.Message.Price);
    }
}
