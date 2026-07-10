namespace XgpSaveTools.SaveHandlers;

/// <summary>
/// Marks legacy handlers whose exported files are transformed or synthetic and cannot safely
/// be copied directly back into their source WGS blobs.
/// </summary>
public interface IExportOnlySaveHandler
{
}
