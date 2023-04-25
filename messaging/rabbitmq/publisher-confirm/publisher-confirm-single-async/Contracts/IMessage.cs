namespace PublisherConfirmAsync.Contracts;

public interface IMessage
{
    Guid MessageId => Guid.NewGuid();
}
