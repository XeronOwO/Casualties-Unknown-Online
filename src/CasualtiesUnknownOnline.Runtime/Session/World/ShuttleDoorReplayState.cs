namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The shuttle-door replay's elapsed-time projection — extracted from the
/// adapter's TrapVisualReplay so the mapping is unit-testable: a late joiner's
/// replay lands at the CURRENT state instead of re-running the opening
/// animation from zero. The door's Update drives the same mapping live
/// (ShuttleStartOpen.cs: progress accumulates, the 2 s pre-warning sound fires
/// past progress 2, the talk past 4) — the replay just jumps to the point.
/// </summary>
internal static class ShuttleDoorReplayState
{
	/// <summary>The door's state at the given elapsed seconds since its trigger.</summary>
	internal static (float Progress, bool PlayedSound, bool DidTalk) FromElapsed(float elapsedSeconds) =>
		(elapsedSeconds, elapsedSeconds > 2f, elapsedSeconds > 4f);

	/// <summary>
	/// Live relays (elapsed 0) replay the collision-only trigger sound;
	/// late-joiner snapshots (elapsed &gt; 0) jump to the current state without
	/// replaying old sounds (the host's door is not re-playing its opening
	/// either).
	/// </summary>
	internal static bool ShouldReplayTriggerSound(float elapsedSeconds) => elapsedSeconds <= 0f;
}
