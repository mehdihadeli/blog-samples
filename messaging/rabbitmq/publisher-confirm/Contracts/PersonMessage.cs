namespace PublisherConfirm.Contracts;

public class PersonMessage : IMessage
{
	public required string FirstName { get; set; }
	public required string LastName { get; set; }
}