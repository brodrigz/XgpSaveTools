using XgpSaveTools.Extensions;
using XgpSaveTools.Operations;

namespace Xgpst_ConsoleApp.Operations;

public sealed class ConsoleOperationPresenter
{
	private readonly ConsoleHelper _helper;

	public ConsoleOperationPresenter(ConsoleHelper helper)
	{
		_helper = helper;
	}

	public bool PresentAndConfirm(OperationPlan plan)
	{
		Console.WriteLine();
		switch (plan)
		{
			case ExportPlan export:
				foreach (var warning in export.Warnings ?? Array.Empty<string>()) _helper.WriteWarning(warning);
				Console.WriteLine($"Files to export: {export.Files.Count}");
				foreach (var file in export.Files)
				{
					var size = File.Exists(file.PreparedFile)
						? IoExtensions.GetReadableFileSize(new FileInfo(file.PreparedFile).Length)
						: "missing";
					Console.WriteLine($"  - {file.OutputName} ({size})");
				}
				if (export.Warnings == null || export.Warnings.Count == 0) return true;
				Console.WriteLine();
				var exportChoice = _helper.SelectOption(
					new[] { "Continue", "Cancel" },
					"Create this archive?",
					x => x,
					disableGoBack: true);
				return exportChoice.Key == 0;

			case ImportPlan import:
				foreach (var warning in import.Warnings) _helper.WriteWarning(warning);
				Console.WriteLine($"WGS changes: {import.Mutations.Count}");
				foreach (var mutation in import.Mutations)
				{
					var action = mutation is PlannedDeletion ? "Delete" : "Replace";
					Console.WriteLine($"  - {action} {mutation.Target.ContainerName}/{mutation.Target.FileName}");
				}
				Console.WriteLine();
				var choice = _helper.SelectOption(new[] { "Continue", "Cancel" }, "Apply these changes?", x => x, disableGoBack: true);
				return choice.Key == 0;

			default:
				throw new NotSupportedException($"Unsupported operation plan: {plan.GetType().Name}");
		}
	}
}
