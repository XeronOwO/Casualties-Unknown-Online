using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod message channel (NetMsg.ModMessage — the shared mod-message frame
/// carries the sending mod's id + the sending mod's own value; the receiving
/// side routes by id to the locally-loaded mod with that id, dropping unknown
/// ids with a log). Semantics are report/定向, star topology, NO auto-relay: a
/// guest's SendToHost reaches the host's copy of the mod only; broadcasting is
/// the host-side mod's explicit call.
///
/// The value is a <see cref="ModValue"/> — CUO's own typed data model — and not
/// an opaque byte array: the framework can validate it, bound it structurally,
/// log it and show it, and it never has to guess what a blob meant. A mod that
/// wants its own compact encoding puts the bytes in the model's binary leaf, on
/// purpose. A value that cannot be encoded inside the framework's 64 KiB rail
/// (or that breaks a structural budget: depth, entry count, text size) is
/// refused with one log line naming the reason and the path inside the model.
/// </summary>
public interface IModNetwork
{
	/// <summary>
	/// Guest: report a value to the host's copy of this mod (no-op on the
	/// host — a host mod talks to itself locally). No-op outside a session and
	/// for a value the framework cannot encode, which is logged.
	/// </summary>
	void SendToHost(ModValue value);

	/// <summary>
	/// Host only: send a value to one member's copy of this mod (no-op for a
	/// guest — the star has no peer channels). No-op outside a session and for a
	/// value the framework cannot encode, which is logged.
	/// </summary>
	void SendToPeer(ulong steamId, ModValue value);

	/// <summary>
	/// Host only: broadcast a value to every member's copy of this mod
	/// (including the host's own — a mod receiving its own broadcast is how
	/// "all sides run this" is expressed). No-op outside a session and for a
	/// value the framework cannot encode, which is logged.
	/// </summary>
	void Broadcast(ModValue value);

	/// <summary>A value from another member's copy of this mod (senderSteamId, value).</summary>
	event Action<ulong, ModValue>? MessageReceived;
}
