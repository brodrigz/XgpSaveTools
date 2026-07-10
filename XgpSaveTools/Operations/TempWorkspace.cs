namespace XgpSaveTools.Operations;

public interface ITempWorkspace : IDisposable
{
	string RootPath { get; }
	string GetPath(string relativePath);
}

public sealed class TempWorkspace : ITempWorkspace
{
	private bool _disposed;
	public string RootPath { get; }

	public TempWorkspace()
	{
		RootPath = Directory.CreateDirectory(
			Path.Combine(Path.GetTempPath(), "XgpSaveTools", Path.GetRandomFileName()))
			.FullName;
	}

	public string GetPath(string relativePath)
	{
		if (_disposed) throw new ObjectDisposedException(nameof(TempWorkspace));
		if (string.IsNullOrWhiteSpace(relativePath))
			throw new ArgumentException("A relative path is required.", nameof(relativePath));

		var root = Path.GetFullPath(RootPath) + Path.DirectorySeparatorChar;
		var result = Path.GetFullPath(Path.Combine(RootPath, relativePath));
		if (!result.StartsWith(root, StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException("Temporary path escapes the workspace.");

		var parent = Path.GetDirectoryName(result);
		if (parent != null) Directory.CreateDirectory(parent);
		return result;
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		try
		{
			if (Directory.Exists(RootPath)) Directory.Delete(RootPath, true);
		}
		catch
		{
			// Temporary cleanup is best effort. Existing global cleanup remains as a fallback.
		}
	}
}
