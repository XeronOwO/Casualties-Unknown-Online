using System;
using System.Collections.Generic;
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

	/// <summary>Host only: a block changed after generation (mined/destroyed/built) — record it in the damage table.</summary>
	void ReportBlockState(int x, int y, ushort block);

	/// <summary>Host only: a new world layer is generating — the damage table starts empty again.</summary>
	void ResetDamagedBlocks();

	/// <summary>Host only: send the full damage table to one member (on its world entry).</summary>
	void SendBlockStateSnapshot(ulong targetSteamId);

	void FireBlockStateReceived(IReadOnlyList<DamagedBlock> blocks);

	event Action<IReadOnlyList<DamagedBlock>>? BlockStateReceived;
}
