namespace XgpSaveTools.Operations;

public sealed class OperationArguments
{
	private readonly IReadOnlyDictionary<string, object?> _values;

	public OperationArguments(IReadOnlyDictionary<string, object?> values)
	{
		_values = values;
	}

	public string GetRequiredString(string key)
	{
		if (!_values.TryGetValue(key, out var value) || value is not string text || string.IsNullOrWhiteSpace(text))
			throw new ArgumentException($"Missing required operation argument '{key}'.", nameof(key));
		return text;
	}

	public string? GetString(string key)
	{
		return _values.TryGetValue(key, out var value) ? value as string : null;
	}

	public bool GetBoolean(string key, bool defaultValue = false)
	{
		return _values.TryGetValue(key, out var value) && value is bool result
			? result
			: defaultValue;
	}

	public void Validate(IReadOnlyList<OperationParameter> parameters)
	{
		foreach (var parameter in parameters)
		{
			_values.TryGetValue(parameter.Key, out var value);
			var error = parameter.Validate(value);
			if (error != null) throw new ArgumentException(error, parameter.Key);
		}
	}
}
