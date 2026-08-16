namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The character-presentation half of <see cref="GameAdapter"/>'s IPatchBridge
/// surface (the partial split at the 600-line gate): the limb-latch event
/// forward. The state stays in the main partial declaration.
/// </summary>
public sealed partial class GameAdapter
{
	void IPatchBridge.OnLimbStateEvent(Limb limb) => _characterDataSync.ReportLimbStateEvent(limb);
}
