namespace PublisherConfirmBatchAsync.Contracts;

public interface IMessage
{
    Guid MessageId => Guid.NewGuid();
}
