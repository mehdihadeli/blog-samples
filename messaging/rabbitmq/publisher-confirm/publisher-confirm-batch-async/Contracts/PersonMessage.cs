namespace PublisherConfirmBatchAsync.Contracts;

public class PersonMessage : IMessage
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
}
