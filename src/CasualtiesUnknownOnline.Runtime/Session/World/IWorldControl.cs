using System;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world surface packet handlers operate on — implemented by WorldService.
/// Handlers depend on this narrow interface instead of the concrete service,
/// which keeps the constructor graph acyclic (abstract extraction, user rule).
/// </summary>
public interface IWorldControl
{
	WorldStartParams? WorldParams { get; set; }

	void FireBlockDamagedReceived(NetVector2 pos, float damage);

	event Action<NetVector2, float>? BlockDamagedReceived;

	void FireWorldJoinReceived();

	event Action? WorldJoinReceived;
}
