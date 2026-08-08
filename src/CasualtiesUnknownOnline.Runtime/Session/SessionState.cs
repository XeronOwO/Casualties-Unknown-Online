using System;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Session-scoped state shared between the session layer and the entity/data
/// domains: whether the session is active, the local scene state, and the
/// session-level events. Owned by SessionService (it writes the flags, fires
/// the events); the domains read it — extracted so they depend on this object
/// instead of on SessionService itself (acyclic constructor graph, user rule:
/// abstract extraction, never AttachXxx wiring).
/// </summary>
public sealed class SessionState
{
	/// <summary>True once the handshake completed (protocol versions agreed). Set by the handshake handlers.</summary>
	public bool SessionActive { get; set; }

	/// <summary>Local scene state — true while the local player is in the world (the SceneState we last reported).</summary>
	public bool LocalInWorld { get; set; }

	/// <summary>Raised when the handshake completes and scene exchange can start (first member only).</summary>
	public event Action? SessionActivated;

	/// <summary>Raised when the session ends (all members gone, lobby left, …).</summary>
	public event Action? SessionEnded;

	public void FireSessionActivated() => SessionActivated?.Invoke();

	public void FireSessionEnded() => SessionEnded?.Invoke();
}
