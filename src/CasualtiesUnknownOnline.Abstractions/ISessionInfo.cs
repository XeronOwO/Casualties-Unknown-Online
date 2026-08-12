using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// A read-only snapshot of the session at the moment the mod was bound (see
/// <see cref="IModContext.Session"/> — it does NOT stay live; re-read events
/// for increments). SteamIds are ulong per the Steam 64-bit id space.
/// </summary>
public interface ISessionInfo
{
	bool IsHost { get; }

	bool SessionActive { get; }

	ulong LocalSteamId { get; }

	ulong HostSteamId { get; }

	/// <summary>
	/// The peer member set (the local peer is <see cref="LocalSteamId"/>, not
	/// listed here) — the same semantics as the session's broadcast fan-out.
	/// </summary>
	IReadOnlyList<ulong> MemberSteamIds { get; }
}
