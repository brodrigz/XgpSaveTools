namespace XgpSaveTools.SaveHandlers.Impl.DoomDarkAges;

public sealed class DoomEternalHandler : DoomIdTechHandler
{
	public const string HandlerId = "doom-eternal";

	public DoomEternalHandler()
		: base(HandlerId, "DOOM Eternal", "doom_eternal", checksumSidecarLength: sizeof(uint))
	{
	}
}
