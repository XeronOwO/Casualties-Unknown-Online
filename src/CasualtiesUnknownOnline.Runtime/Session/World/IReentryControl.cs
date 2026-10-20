using System;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world re-entry request's control surface packet handlers operate on — implemented by
/// <see cref="ReentryRequestChannel"/>. Separate from <see cref="IWorldControl"/> for the reason
/// the end-of-layer control is: one small domain (a member asking to be let back in, and the
/// host's facts that bound the ask) stays independently testable instead of widening the world
/// facade.
/// <para>
/// The need is the entry instruction's shape: <c>WorldJoin</c> is an EDGE the host sends once per
/// entry, and a member can miss it or discard it against its own state (an invite that arrives
/// while the member is still in its old world waits at the entry gate and dies with that world).
/// The member therefore reports "take me back in" and the host answers with the same instruction,
/// so the entry becomes convergent instead of one-shot. It is the member's own intent — a member
/// that left the world deliberately does not ask — and the host decides with its own facts.
/// </para>
/// </summary>
public interface IReentryControl
{
	/// <summary>
	/// Any role: ask the host to re-issue this member's world-entry instruction. Returns true when
	/// the request left this client — guest in a live session. False means nothing was sent (a host
	/// or solo player's own entry is its own click, and a session that is not active has no host to
	/// ask), and the caller must not spend its bounded window on it.
	/// </summary>
	bool TrySendReentryRequest();

	/// <summary>Host only: a member says it is out of the world after the host's own exit and asks to be taken back in. It is admitted only from a handshaken member this host holds OUT of the world; what it then means for the world (the targeted entry instruction, once per cooldown) is the world domain's decision.</summary>
	void HandleReentryRequest(ulong sender);

	/// <summary>Host only: a member's re-entry ask was admitted against this host's own membership facts — the world half is this host's to answer.</summary>
	event Action<ulong>? ReentryRequested;
}
