using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The remote-inventory drag half of the Harmony patch bridge: the release
/// window's local identity, its unresolved-proxy report and the one call that
/// turns a closed bracket into host-validated intents. Kept as its own
/// interface so <see cref="IPatchBridge"/> stays under the architecture line
/// gate while the patches keep exactly one seam to report through.
/// </summary>
internal interface IRemoteBackpackPatchBridge
{
	/// <summary>The local player's SteamId — the destination identity a slot release produces when the ring is back on the local body.</summary>
	ulong LocalSteamId { get; }

	/// <summary>A display proxy was released but carries no authoritative identity, so the gesture cannot become an intent — reported, never silently dropped.</summary>
	void ReportRemoteDragUnresolved(Item dragItem);

	/// <summary>
	/// One closed release bracket: log the native calls this stage could not
	/// name or could not carry, log the unclassified gesture when the release
	/// produced nothing, and send the captured intents.
	/// </summary>
	void EmitRemoteDragIntents(RemoteDragOutcome outcome);

	/// <summary>A gesture on a display proxy that no intent carries yet was refused — logged, never a silent no-op.</summary>
	void ReportRemoteGestureNotCarried(string gesture);
}
