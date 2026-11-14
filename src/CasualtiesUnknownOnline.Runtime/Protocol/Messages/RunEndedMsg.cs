using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The host's world ended and its members must leave it too.
///
/// Measured 2026-11-14: the host leaves the world through the game's own
/// "return to the menu" action; its own scene unloads, but a member's world keeps
/// running with nobody to play with — its page reads Home while its state still
/// says in world, and the clone of the departed host keeps updating. The session
/// mechanism has no run-end notion at all: <c>RunEndingTracker</c> judges only the
/// game's own endings and <c>WorldEntryFanout</c> sends world facts on entry only.
///
/// The host sends this once, after the leave has actually been performed; the
/// member answers it through the same leave path it uses for its own exit, so no
/// cut is written on its side and the world's objects unload with the scene.
/// </summary>
[ProtoContract]
public sealed class RunEndedMsg
{
	/// <summary>The host's run id, or zero when the host could not name it.</summary>
	[ProtoMember(1)]
	public long RunId { get; set; }
}
