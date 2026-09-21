using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The session facts the kernel replication surface reads: which side this
/// client is, whether a session is live, who the local and host peers are, and
/// the peers a broadcast reaches.
///
/// <para>
/// It is deliberately narrower than the Runtime's session control surface: the
/// role arrives as two sides rather than the session's own vocabulary, and the
/// member table arrives as the peer ids a broadcast needs — so a member's
/// presence record cannot leak into this layer. <see cref="RoleName"/> exists
/// only so the audit lines that name the role keep saying what they said before
/// the move.
/// </para>
/// </summary>
public interface IKernelSessionFacts
{
	/// <summary>This client is the host of a live session.</summary>
	bool IsHost { get; }

	/// <summary>This client joined as a guest of a live session.</summary>
	bool IsGuest { get; }

	/// <summary>A session is active on this side (the host or guest role is held).</summary>
	bool SessionActive { get; }

	ulong LocalSteamId { get; }

	/// <summary>The host's steam id; 0 while no host is known.</summary>
	ulong HostSteamId { get; }

	/// <summary>
	/// The handshaken peers other than this client — the members a host-side
	/// broadcast addresses. Filtering happens on the session side so this layer
	/// never sees a presence record.
	/// </summary>
	IEnumerable<ulong> HandshakenPeerIds { get; }

	/// <summary>The session side's display name ("Host", "Guest", "None") — log context only.</summary>
	string RoleName { get; }

	/// <summary>Raised when the session ends; the kernel replication surface resets its session state on it.</summary>
	event Action? SessionEnded;
}
