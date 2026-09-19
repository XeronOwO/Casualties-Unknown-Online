using System;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world-time control surface packet handlers operate on — implemented by
/// WorldTimeChannel. Separate from IWorldControl so the already-large world
/// service stays untouched and the time domain stays independently testable.
/// </summary>
public interface IWorldTimeControl
{
	/// <summary>Guest only: report a speed the local client has ALREADY applied (hotkey, or the native left/right movement reset) — the host arbitrates it and answers through the broadcast.</summary>
	void SendRequest(WorldTimeSpeed speed);

	/// <summary>Host only: broadcast the authoritative speed to every synced member — a change, the world-entry fan-out, the 5 s resend, and the answer to a request (an unchanged answer settles the initiator's pending local initiation).</summary>
	void Broadcast(WorldTimeSpeed speed);

	/// <summary>Host: a guest's world-time request arrived.</summary>
	void FireRequestReceived(ulong sender, WorldTimeSpeed speed);

	/// <summary>Guest: the host's authoritative world-time speed arrived.</summary>
	void FireTimeReceived(WorldTimeSpeed speed);

	event Action<ulong, WorldTimeSpeed>? RequestReceived;

	event Action<WorldTimeSpeed>? TimeReceived;
}
