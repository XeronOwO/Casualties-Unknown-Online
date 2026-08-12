using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod message channel (NetMsg.ModMessage — the shared mod-message frame
/// carries the sending mod's id + a raw payload; the receiving side routes by
/// id to the locally-loaded mod with that id, dropping unknown ids with a
/// log). Semantics are report/定向, star topology, NO auto-relay: a guest's
/// SendToHost reaches the host's copy of the mod only; broadcasting is the
/// host-side mod's explicit call. Payloads over 64 KiB are rejected (framework
/// policy — a reliable-channel safety rail, not a line limit).
/// </summary>
public interface IModNetwork
{
	/// <summary>
	/// Guest: report a payload to the host's copy of this mod (no-op on the
	/// host — a host mod talks to itself locally). No-op outside a session.
	/// </summary>
	void SendToHost(byte[] payload);

	/// <summary>
	/// Host only: send a payload to one member's copy of this mod (no-op for a
	/// guest — the star has no peer channels). No-op outside a session.
	/// </summary>
	void SendToPeer(ulong steamId, byte[] payload);

	/// <summary>
	/// Host only: broadcast a payload to every member's copy of this mod
	/// (including the host's own — a mod receiving its own broadcast is how
	/// "all sides run this" is expressed). No-op outside a session.
	/// </summary>
	void Broadcast(byte[] payload);

	/// <summary>A payload from another member's copy of this mod (senderSteamId, payload).</summary>
	event Action<ulong, byte[]>? MessageReceived;
}
