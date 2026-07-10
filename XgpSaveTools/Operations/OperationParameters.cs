namespace XgpSaveTools.Operations;

public abstract record OperationParameter(
	string Key,
	string Label,
	string? Description,
	bool Required)
{
	public abstract string? Validate(object? value);
}

public sealed record TextParameter(
	string ParameterKey,
	string ParameterLabel,
	string? ParameterDescription = null,
	bool IsRequired = true,
	string? DefaultValue = null,
	Func<string, string?>? Validator = null)
	: OperationParameter(ParameterKey, ParameterLabel, ParameterDescription, IsRequired)
{
	public override string? Validate(object? value)
	{
		var text = value as string;
		if (string.IsNullOrWhiteSpace(text))
			return Required ? $"{Label} is required." : null;
		return Validator?.Invoke(text);
	}
}

public sealed record FileParameter(
	string ParameterKey,
	string ParameterLabel,
	string? ParameterDescription = null,
	bool IsRequired = true)
	: OperationParameter(ParameterKey, ParameterLabel, ParameterDescription, IsRequired)
{
	public override string? Validate(object? value)
	{
		var path = value as string;
		if (string.IsNullOrWhiteSpace(path))
			return Required ? $"{Label} is required." : null;
		return File.Exists(path) ? null : $"File not found: {path}";
	}
}

public sealed record DirectoryParameter(
	string ParameterKey,
	string ParameterLabel,
	string? ParameterDescription = null,
	bool IsRequired = true)
	: OperationParameter(ParameterKey, ParameterLabel, ParameterDescription, IsRequired)
{
	public override string? Validate(object? value)
	{
		var path = value as string;
		if (string.IsNullOrWhiteSpace(path))
			return Required ? $"{Label} is required." : null;
		return Directory.Exists(path) ? null : $"Directory not found: {path}";
	}
}

public sealed record ChoiceOption(string Label, string Value);

public sealed record ChoiceParameter(
	string ParameterKey,
	string ParameterLabel,
	IReadOnlyList<ChoiceOption> Options,
	string? ParameterDescription = null,
	bool IsRequired = true)
	: OperationParameter(ParameterKey, ParameterLabel, ParameterDescription, IsRequired)
{
	public override string? Validate(object? value)
	{
		var selected = value as string;
		if (string.IsNullOrWhiteSpace(selected))
			return Required ? $"{Label} is required." : null;
		return Options.Any(x => x.Value == selected)
			? null
			: $"Invalid selection for {Label}.";
	}
}

public sealed record BooleanParameter(
	string ParameterKey,
	string ParameterLabel,
	bool DefaultValue = false,
	string? ParameterDescription = null)
	: OperationParameter(ParameterKey, ParameterLabel, ParameterDescription, true)
{
	public override string? Validate(object? value) => value is bool
		? null
		: $"{Label} must be either yes or no.";
}
