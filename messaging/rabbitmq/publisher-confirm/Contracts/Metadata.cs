namespace PublisherConfirm.Contracts;

public class Metadata : Dictionary<string, object?>, IMetadata
{
	private Metadata()
	{
	}
	public Metadata(IDictionary<string, object?> dictionary) : base(dictionary)
	{
	}

	public static IMetadata FromMeta(Metadata? metadata) => metadata == null ? new Metadata() : new Metadata(metadata);

	public static IMetadata FromHeaders(Dictionary<string, string?>? headers)
		=> headers == null ? new Metadata() : new Metadata(headers.ToDictionary(x => x.Key, x => (object?)x.Value));

	public IMetadata With<T>(string key, T? value) {
		if (value != null) this[key] = value;
		return this;
	}

	public Dictionary<string, string?> ToHeaders() => this.ToDictionary(x => x.Key, x => x.Value?.ToString());

	public string? GetString(string key) => TryGetValue(key, out var value) ? value?.ToString() : default;

	public T? Get<T>(string key) => TryGetValue(key, out var value) && value is T v ? v : default;

	public IMetadata Add(string key, string? value) {
		if (!string.IsNullOrEmpty(value)) this[key] = value;
		return this;
	}
}