namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Session-ended resets for the adapter-side pending operations (partial split
/// at the 600-line gate): a pending drop/break/craft claim must never flush
/// into the next lobby, a pending character restore must never apply to its
/// body, and render clones must not outlive their session.
/// </summary>
public sealed partial class GameAdapter
{
	private void OnSessionEnded()
	{
		_characterDataSync.ResetSessionState();
		_itemWorldSync.ResetPending();
		_blockBreakSync.ResetPending();
		_craftingSync.ResetPending();
		_heaterCookSync.Reset();
		_gate.ResetSessionState();
		_renderer.DestroyAllClones();
	}
}
