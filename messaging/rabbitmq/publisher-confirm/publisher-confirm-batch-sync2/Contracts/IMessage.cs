namespace PublisherConfirmBatchSync.Contracts;

public interface IMessage
{
    Guid MessageId => Guid.NewGuid();
}
