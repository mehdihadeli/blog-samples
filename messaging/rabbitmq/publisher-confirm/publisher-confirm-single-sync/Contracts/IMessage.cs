namespace PublisherConfirmSingleSync.Contracts;

public interface IMessage
{
    Guid MessageId => Guid.NewGuid();
}
