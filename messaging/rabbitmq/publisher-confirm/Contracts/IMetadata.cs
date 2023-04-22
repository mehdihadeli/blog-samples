namespace PublisherConfirm.Contracts;

public interface IMetadata : IDictionary<string, object?>
{
	Dictionary<string, string?> ToHeaders();
	string? GetString(string key);
	IMetadata With<T>(string key, T? value);
	T? Get<T>(string key);
	IMetadata Add(string key, string? value);
}