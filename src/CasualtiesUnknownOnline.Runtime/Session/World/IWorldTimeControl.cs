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
	/// <summary>Guest only: report the local speed hotkey/movement-reset intent to the host (the host applies policy).</summary>
	void SendRequest(WorldTimeSpeed speed);

	/// <summary>Host only: broadcast the authoritative world-time speed to every synced member.</summary>
	void Broadcast(WorldTimeSpeed speed);

	/// <summary>Host: a guest's world-time request arrived.</summary>
	void FireRequestReceived(ulong sender, WorldTimeSpeed speed);

	/// <summary>Guest: the host's authoritative world-time speed arrived.</summary>
	void FireTimeReceived(WorldTimeSpeed speed);

	event Action<ulong, WorldTimeSpeed>? RequestReceived;

	event Action<WorldTimeSpeed>? TimeReceived;
}
