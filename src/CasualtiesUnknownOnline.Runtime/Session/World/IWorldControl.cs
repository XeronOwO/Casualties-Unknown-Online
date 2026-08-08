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

	/// <summary>
	/// Report a locally-performed player attack on a building entity (local
	/// compute): guest → host as a report (the host applies the damage to its
	/// own copy — which rolls the host-side entity drops — and relays), host →
	/// guest as a broadcast relay. The entity is identified by world position
	/// (world entities are generated deterministically on both sides).
	/// </summary>
	void SendBuildingEntityDamaged(NetVector2 pos, float damage);

	/// <summary>Guest: a block was placed locally — report it to the host (host arbitrates + relays).</summary>
	void SendBlockPlacedReport(int x, int y, ushort block);

	/// <summary>Host only: broadcast a placed block (source excluded — it already placed locally).</summary>
	void BroadcastBlockPlaced(ulong excludeSteamId, int x, int y, ushort block);

	void FireBlockPlacedReceived(ulong sender, int x, int y, ushort block);

	event Action<ulong, int, int, ushort>? BlockPlacedReceived;

	void FireBuildingEntityDamagedReceived(NetVector2 pos, float damage);

	/// <summary>A player's attack damaged a building entity — apply the damage to the entity at Pos.</summary>
	event Action<NetVector2, float>? BuildingEntityDamagedReceived;

	/// <summary>
	/// Report a locally-opened lockable entity (instant-open/lockpick/keypad —
	/// all write health = 0 directly): guest → host as a report (the host applies
	/// the open to its copy, which rolls the host-side drops, and relays), host →
	/// guest as a broadcast relay.
	/// </summary>
	void SendBuildingEntityOpened(NetVector2 pos);

	void FireBuildingEntityOpenedReceived(NetVector2 pos);

	/// <summary>A lockable entity was opened — apply the open (health = 0) to the entity at Pos.</summary>
	event Action<NetVector2>? BuildingEntityOpenedReceived;

	/// <summary>Host only: everyone enters the world together — arm the start gate (waits for every guest's InWorld, or 30 s). Returns whether anyone is being waited on.</summary>
	bool StartStartGate();

	/// <summary>Host only: a member finished loading (InWorld) — release the gate when all are in, or let a late joiner pass directly.</summary>
	void NotifyMemberInWorld(ulong steamId);

	/// <summary>Host only: the gate is armed (the host is waiting too) — driver pumps this for the 30 s fallback.</summary>
	void MaybeForceStartGate();

	/// <summary>Host only: true while the host itself must wait (frozen + overlay).</summary>
	bool StartGateActive { get; }

	/// <summary>Host only: seconds left until the gate force-releases (0 when not armed).</summary>
	int StartGateRemainingMs { get; }

	void FireWorldReadyReceived();

	event Action? WorldReadyReceived;

	/// <summary>Host only: a block changed after generation (mined/destroyed/built) — upsert it into the damage table.</summary>
	void ReportBlockState(int x, int y, ushort block);

	/// <summary>Host only: a block was restored to its generated baseline — drop it from the damage table.</summary>
	void RemoveBlockState(int x, int y);

	/// <summary>Host only: a new world layer is generating — the damage table starts empty again.</summary>
	void ResetDamagedBlocks();

	/// <summary>Host only: send the full damage table to one member (on its world entry).</summary>
	void SendBlockStateSnapshot(ulong targetSteamId);

	void FireBlockStateReceived(IReadOnlyList<DamagedBlock> blocks);

	event Action<IReadOnlyList<DamagedBlock>>? BlockStateReceived;
}
