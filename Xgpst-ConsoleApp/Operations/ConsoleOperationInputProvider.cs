using XgpSaveTools.Operations;

namespace Xgpst_ConsoleApp.Operations;

public sealed class ConsoleOperationInputProvider : IOperationInputProvider
{
	private readonly ConsoleHelper _helper;

	public ConsoleOperationInputProvider(ConsoleHelper helper)
	{
		_helper = helper;
	}

	public Task<OperationArguments?> CollectAsync(
		IReadOnlyList<OperationParameter> parameters,
		CancellationToken cancellationToken)
	{
		var values = new Dictionary<string, object?>();
		foreach (var parameter in parameters)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!string.IsNullOrWhiteSpace(parameter.Description))
				Console.WriteLine(parameter.Description);

			object? value = parameter switch
			{
				TextParameter text => ReadText(text),
				FileParameter file => _helper.ReadValidFile(file.Label + ":").FullName,
				DirectoryParameter directory => _helper.ReadValidDirectory(directory.Label + ":"),
				ChoiceParameter choice => ReadChoice(choice),
				BooleanParameter boolean => ReadBoolean(boolean),
				_ => throw new NotSupportedException($"Unsupported operation parameter: {parameter.GetType().Name}")
			};

			if (value is CancelledValue) return Task.FromResult<OperationArguments?>(null);
			values[parameter.Key] = value;
			Console.WriteLine();
		}

		var arguments = new OperationArguments(values);
		arguments.Validate(parameters);
		return Task.FromResult<OperationArguments?>(arguments);
	}

	private string? ReadText(TextParameter parameter)
	{
		while (true)
		{
			var defaultLabel = parameter.DefaultValue == null ? string.Empty : $" [{parameter.DefaultValue}]";
			Console.Write($"{parameter.Label}{defaultLabel}: ");
			var value = Console.ReadLine()?.Trim();
			if (string.IsNullOrEmpty(value)) value = parameter.DefaultValue;
			var error = parameter.Validate(value);
			if (error == null) return value;
			_helper.WriteError(error);
		}
	}

	private object ReadChoice(ChoiceParameter parameter)
	{
		var selected = _helper.SelectOption(
			parameter.Options.ToList(),
			parameter.Label + ":",
			x => x.Label);
		return selected.Key == -1 ? CancelledValue.Instance : selected.Value!.Value;
	}

	private object ReadBoolean(BooleanParameter parameter)
	{
		var options = parameter.DefaultValue
			? new[] { (Label: "Yes", Value: true), (Label: "No", Value: false) }
			: new[] { (Label: "No", Value: false), (Label: "Yes", Value: true) };
		var selected = _helper.SelectOption(options, parameter.Label + ":", x => x.Label);
		return selected.Key == -1 ? CancelledValue.Instance : selected.Value.Value;
	}

	private sealed class CancelledValue
	{
		public static readonly CancelledValue Instance = new();
		private CancelledValue() { }
	}
}
