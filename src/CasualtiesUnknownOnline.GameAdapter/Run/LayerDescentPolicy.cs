using CasualtiesUnknownOnline.Runtime.Session;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The host descent's decisions, without the game: may this member follow the edge,
/// has its own new-layer generation settled, and must the follow be abandoned. Kept
/// pure so the rules have a test host — <see cref="RunCoordinator"/> reads
/// <c>WorldGeneration</c> and <c>PlayerCamera</c> directly and cannot be constructed
/// outside the game.
/// </summary>
internal static class LayerDescentPolicy
{
	/// <summary>A follow whose baseline never arrived is abandoned after this long (the normal wait is under a second).</summary>
	internal const int HoldTimeoutMs = 30_000;

	/// <summary>A follow whose generation never settles is abandoned after this long (a layer generates in ~10 s).</summary>
	internal const int SettleTimeoutMs = 180_000;

	/// <summary>
	/// May this side start its own new-layer generation for the host's edge? Only a
	/// guest that is PLAYING (or already waiting on this descent's gate, when the host
	/// advances again) in a live world it still has can: a member in the menu is not
	/// the host's to pull through this path (the ordinary entry invites it), and one
	/// whose own generation is still in flight has the same layer in flight.
	/// </summary>
	internal static LayerDescentDecision DecideFollow(
		SessionRole role,
		bool sessionActive,
		bool inWorld,
		bool playingOrWaiting,
		bool generationInFlight,
		bool hasWorld)
	{
		if (role != SessionRole.Guest || !sessionActive)
		{
			return LayerDescentDecision.NotThisSides;
		}

		if (generationInFlight)
		{
			return LayerDescentDecision.AlreadyDescending;
		}

		if (!inWorld || !playingOrWaiting)
		{
			return LayerDescentDecision.NotPlaying;
		}

		return hasWorld ? LayerDescentDecision.Follow : LayerDescentDecision.NoWorld;
	}

	/// <summary>
	/// Has the member's own new-layer generation settled? Three facts, and the launch
	/// fact is what keeps the game's transient "world object exists, generatingWorld
	/// down" frame of the baseline hold from reading as a settled world: the
	/// generation must have STARTED (the wrapper left its hold), it must not still be
	/// running (generating or regenerating), and a live world must be back.
	/// </summary>
	internal static bool IsSettled(bool generationStarted, bool generating, bool regenerating, bool liveWorld) =>
		generationStarted && !generating && !regenerating && liveWorld;

	/// <summary>
	/// Must the descent follow be abandoned? Two ways out besides the settle: the world
	/// object is gone (the member left through a real leave or the death menu — the
	/// scene load destroys it), or one of the follow's own waits outlived its bound
	/// (the host's baseline never arrived, or the generation never settled). Abandoning
	/// hands the member back to the ordinary entry path instead of leaving a phase that
	/// refuses every WorldJoin for the rest of the session.
	/// </summary>
	internal static bool ShouldAbort(bool hasWorld, bool holdTimedOut, bool settleTimedOut) =>
		!hasWorld || holdTimedOut || settleTimedOut;
}
