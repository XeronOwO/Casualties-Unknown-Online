namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The base composition root's answer to <see cref="IWearSemantics"/>: with no game
/// data nothing is a wearable, so the one-shot use path refuses every wear gesture by
/// name instead of approximating one. The plugin replaces it with the Game Adapter's
/// registry-backed implementation, the same default-and-replace shape the other two
/// content seams use.
/// </summary>
public sealed class NoWearSemantics : IWearSemantics
{
	public bool TryGetWearPlacement(string itemId, out int limbIndex, out string wearSlotId)
	{
		limbIndex = -1;
		wearSlotId = "";
		return false;
	}
}
