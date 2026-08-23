using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The block/building/world-state message-flow surface of <see cref="WorldService"/>
/// (split off at the 600-line gate): events, report/send/broadcast plumbing and
/// the block-difference snapshot for late joiners. The world-defining state and
/// the start-gate lifecycle stay in WorldService.cs; the entity-channel and
/// block-damage snapshot surfaces live in their existing partials.
/// </summary>
public sealed partial class WorldService
{
	/// <summary>
	/// Host: a guest reported damage — apply + relay (the drops ride the message,
	/// the host arbitrates the break: first-writer-wins, the loser's drops are
	/// refused). Guest: the host's broadcast — apply. Drops are null/empty when
	/// the damage did not break the block. MetalBonus preserves the game's
	/// ×10 metallic-block multiplier (WorldGeneration.cs:715).
	/// </summary>
	public event Action<ulong, NetVector2, float, bool, IReadOnlyList<BlockDropEntryMsg>?>? BlockDamagedReceived;

	public void FireBlockDamagedReceived(ulong sender, NetVector2 pos, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops) =>
		BlockDamagedReceived?.Invoke(sender, pos, damage, metalBonus, drops);

	/// <summary>Guest: the host told us to enter the world — isTutorial = follow StartTutorial (it nulls runSettings itself), else StartRun.</summary>
	public event Action<bool>? WorldJoinReceived;

	public void FireWorldJoinReceived(bool isTutorial) => WorldJoinReceived?.Invoke(isTutorial);

	/// <summary>Guest: the host's authoritative block-state snapshot arrived (world entry).</summary>
	public event Action<IReadOnlyList<DamagedBlock>>? BlockStateReceived;

	public event Action<float, float>? EarthquakeStartReceived;

	/// <summary>Guest: the host's keypad codes arrived (position-keyed Openables).</summary>
	public event Action<IReadOnlyList<KeypadEntryMsg>>? KeypadCodeReceived;

	public void FireKeypadCodeReceived(IReadOnlyList<KeypadEntryMsg> codes) => KeypadCodeReceived?.Invoke(codes);

	/// <summary>Guest: the host's geyser liquid types arrived (position-keyed GeyserScripts).</summary>
	public event Action<IReadOnlyList<GeyserStateEntryMsg>>? GeyserStateReceived;

	public void FireGeyserStateReceived(IReadOnlyList<GeyserStateEntryMsg> geysers) => GeyserStateReceived?.Invoke(geysers);

	/// <summary>Host only: broadcast the geyser liquid types (position-keyed GeyserScripts — the host's generation-time rolls are the authority) to every synced member.</summary>
	public void SendGeyserStateSnapshot(IReadOnlyList<GeyserStateEntryMsg> geysers)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || geysers.Count == 0)
		{
			return;
		}

		var msg = new GeyserStateSnapshotMsg { Geysers = [.. geysers] };
		foreach (var member in _session.Members)
		{
			if (member.Handshaken)
			{
				_sender.Send(member.SteamId, NetMsg.GeyserStateSnapshot, msg);
			}
		}
	}

	// ---- World entity channels (events + runtime creation + consumptions) —
	// implemented by EntityEventChannel and TradeChannel — see
	// WorldService.Channels.cs (split at the 600-line gate) ----

	/// <summary>Host only: broadcast the keypad codes (position-keyed Openables) to every synced member.</summary>
	public void SendKeypadCodes(IReadOnlyList<KeypadEntryMsg> codes)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || codes.Count == 0)
		{
			return;
		}

		var msg = new KeypadCodeMsg { Codes = [.. codes] };
		foreach (var member in _session.Members)
		{
			if (member.Handshaken)
			{
				_sender.Send(member.SteamId, NetMsg.KeypadCode, msg);
			}
		}
	}

	public void FireBlockStateReceived(IReadOnlyList<DamagedBlock> blocks) => BlockStateReceived?.Invoke(blocks);

	/// <summary>Either side: a block was placed (report up / broadcast down share one message id).</summary>
	public event Action<ulong, int, int, ushort>? BlockPlacedReceived;

	public void FireBlockPlacedReceived(ulong sender, int x, int y, ushort block) =>
		BlockPlacedReceived?.Invoke(sender, x, y, block);

	/// <summary>A building entity was damaged — apply the damage to the entity at Pos. If <c>playHitSound</c> is true the receiver also replays the entity's own hitSound (attack damage); silent damage sources pass false.</summary>
	public event Action<NetVector2, float, bool>? BuildingEntityDamagedReceived;

	public void FireBuildingEntityDamagedReceived(NetVector2 pos, float damage, bool playHitSound) =>
		BuildingEntityDamagedReceived?.Invoke(pos, damage, playHitSound);

	/// <summary>A lockable entity was opened — apply the open (health = 0) to the entity at Pos.</summary>
	public event Action<NetVector2>? BuildingEntityOpenedReceived;

	public void FireBuildingEntityOpenedReceived(NetVector2 pos)
	{
		if (_session.Role == SessionRole.Host)
		{
			_eventChannel.ReportOpenedEntity(pos.X, pos.Y); // a guest's accepted open — recorded for the late-joiner snapshot
		}

		BuildingEntityOpenedReceived?.Invoke(pos);
	}

	/// <summary>
	/// Report a locally-opened lockable entity (instant-open/lockpick/keypad —
	/// all write health = 0 directly, Openable.cs:12 / LockpingMinigame.cs:129 /
	/// KeypadMinigame.cs:138): guest → host as a report (the host applies the
	/// open to its copy — which rolls the host-side drops — and relays), host →
	/// guest as a broadcast relay.
	/// </summary>
	public void SendBuildingEntityOpened(NetVector2 pos)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new BuildingEntityOpenedMsg { Position = pos.ToNetVector2Msg() };
		if (_session.Role == SessionRole.Host)
		{
			_eventChannel.ReportOpenedEntity(pos.X, pos.Y); // the host's own open — recorded for the late-joiner snapshot
			_session.Broadcast(NetMsg.BuildingEntityOpened, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.BuildingEntityOpened, msg);
		}
	}

	/// <summary>
	/// Report a locally-performed building-entity damage (local compute):
	/// guest → host as a report (the host applies the damage to its own copy —
	/// which is what rolls the host-side entity drops — and relays), host →
	/// guest as a broadcast relay. The entity is identified by its world
	/// position (world entities are generated deterministically, so both sides
	/// have the same object at the same place). <paramref name="playHitSound"/>
	/// is true for attack/explosion damage (the receiver replays the entity's
	/// own hitSound) and false for silent damage sources such as cactus
	/// collision self-damage.
	/// </summary>
	public void SendBuildingEntityDamaged(NetVector2 pos, float damage, bool playHitSound = true)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new BuildingEntityDamagedMsg
		{
			Position = pos.ToNetVector2Msg(),
			Damage = damage,
			PlayHitSound = playHitSound,
		};
		if (_session.Role == SessionRole.Host)
		{
			_session.Broadcast(NetMsg.BuildingEntityDamaged, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.BuildingEntityDamaged, msg);
		}
	}

	/// <summary>
	/// Guest side: a block was placed locally (local compute) — report it to
	/// the host, which arbitrates (target must be air) and relays.
	/// </summary>
	public void SendBlockPlacedReport(int x, int y, ushort block)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.BlockPlaced,
			new BlockPlacedMsg { X = x, Y = y, Block = block });
	}

	/// <summary>Host side: broadcast a placed block (source excluded — it already applied locally).</summary>
	public void BroadcastBlockPlaced(ulong excludeSteamId, int x, int y, ushort block)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new BlockPlacedMsg { X = x, Y = y, Block = block };
		_session.BroadcastExcept(excludeSteamId, NetMsg.BlockPlaced, msg);
	}

	public void BroadcastEarthquakeStart(float duration, float nextDelay)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_session.Broadcast(NetMsg.EarthquakeStart, new EarthquakeStartMsg { Duration = duration, NextDelay = nextDelay });
	}

	public void FireEarthquakeStartReceived(float duration, float nextDelay)
	{
		_log.LogInformation("Earthquake started ({Duration:F1}s, next in {NextDelay:F0}s) — showing the effect, re-aligning the quake timer.", duration, nextDelay);
		EarthquakeStartReceived?.Invoke(duration, nextDelay);
	}

	// ---- Radiation line (host authority: the world boundary is host-owned) ----

	/// <summary>The current authoritative radiation-line state (host-only source for world entry/reconnect fan-out).</summary>
	public RadiationLineStateMsg? RadiationLineState { get; private set; }

	public event Action<RadiationLineStateMsg>? RadiationLineStateReceived;

	public void FireRadiationLineStateReceived(RadiationLineStateMsg state) =>
		RadiationLineStateReceived?.Invoke(state);

	/// <summary>Set the world-entry snapshot source (host/solo — no wire send).
	/// A solo host that later creates a lobby already has the current line state
	/// stored, independent of the first live broadcast frame.</summary>
	public void SetRadiationLineState(RadiationLineStateMsg state) => RadiationLineState = state;

	/// <summary>Host only: broadcast the authoritative radiation-line state and
	/// keep it as the world-entry snapshot source.</summary>
	public void BroadcastRadiationLineState(RadiationLineStateMsg state)
	{
		RadiationLineState = state;
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_session.Broadcast(NetMsg.RadiationLineState, state);
		_log.LogDebug("Broadcast radiation-line state active={Active}, timeGone={TimeGone:F2}.", state.Active, state.TimeGone);
	}

	/// <summary>Host only: send the stored radiation-line state to one member
	/// (world entry / reconnect — the world-entry fan-out in HandlerContext).</summary>
	public void SendRadiationLineState(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || RadiationLineState is null)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.RadiationLineState, RadiationLineState);
		_log.LogDebug("Sent radiation-line state (active={Active}, timeGone={TimeGone:F2}) to {Peer}.",
			RadiationLineState.Active, RadiationLineState.TimeGone, targetSteamId);
	}

	/// <summary>
	/// Host only: a block now deviates from its generated baseline (mined,
	/// destroyed, built — the SetBlock write path, which damage application
	/// and earthquakes also go through) — upsert it into the difference table.
	/// </summary>
	public void ReportBlockState(int x, int y, ushort block)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (_damagedBlocks.Count >= MaxDamagedBlocks && !_damagedBlocks.ContainsKey((x, y)))
		{
			return; // cap reached — stop tracking new entries rather than grow unbounded
		}

		_damagedBlocks[(x, y)] = block;
	}

	/// <summary>Host only: a block was restored to its generated baseline — it is no longer part of the difference.</summary>
	public void RemoveBlockState(int x, int y)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_damagedBlocks.Remove((x, y));
	}

	/// <summary>Host only: a new world layer is generating — the tables start empty again (block difference + one-shot trap consumptions + opened entities share the lifecycle).</summary>
	public void ResetDamagedBlocks()
	{
		_damagedBlocks.Clear();
		_blockDamageRegistry.Reset();
		_eventChannel.ResetConsumptions();
		_eventChannel.ResetOpenedEntities();
		_eventChannel.ResetBuildingEntityHealth();
		_eventChannel.ResetTrapLayouts();
	}


	/// <summary>Host only: send the full damage table to one member (on its world entry).</summary>
	public void SendBlockStateSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || _damagedBlocks.Count == 0)
		{
			return;
		}

		var msg = new BlockStateMsg
		{
			Blocks = [.. _damagedBlocks.Select(kv => new BlockStateEntryMsg { X = kv.Key.Item1, Y = kv.Key.Item2, Block = kv.Value })],
		};
		_sender.Send(targetSteamId, NetMsg.WorldBlockState, msg);
		_log.LogInformation("Sent block-state snapshot ({Count} blocks) to {Peer}.", _damagedBlocks.Count, targetSteamId);
	}

	/// <summary>
	/// Host side: tell the members to enter the world. Sent at run-start entry
	/// (the host clicks start — the guest starts its transition immediately,
	/// BEFORE the world params exist; the guest's generation boundary waits for
	/// them) and at handshake time when the host is already in a world (there
	/// the params arrive first, ordered before the join).
	/// </summary>
	public void SendWorldJoin(bool isTutorial)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new WorldJoinMsg { IsTutorial = isTutorial };
		// Members already in the world ignore a re-send anyway, but the second
		// caller (the host's world entry — members that joined mid-generation)
		// targets exactly those that never received the entry WorldJoin.
		foreach (var member in _session.Members)
		{
			if (member.Handshaken && !member.InWorld)
			{
				_sender.Send(member.SteamId, NetMsg.WorldJoin, msg);
			}
		}

		_log.LogInformation("World join sent to {Members} members (tutorial: {Tutorial}).",
			_session.Members.Count(m => m.Handshaken && !m.InWorld), isTutorial);
	}

	/// <summary>Host side: capture and publish world-start parameters (run start).</summary>
	public void PublishWorldParams(WorldStartParams parameters)
	{
		WorldParams = parameters; // the handshake handlers read this when acking a new member
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = parameters.ToWorldStartParamsMsg();
		var members = _session.Members.Where(m => m.Handshaken).ToList();
		foreach (var member in members)
		{
			_sender.Send(member.SteamId, NetMsg.WorldStartParams, msg);
		}

		_log.LogInformation("Published world params ({StateBytes} bytes) to {Members} members.",
			parameters.RandomState.Length, members.Count);
	}

	/// <summary>
	/// Report a locally-performed block damage (local compute): guest → host as
	/// a report (the host arbitrates and relays), host → broadcast to all synced
	/// members (the source excluded on relay — it already applied locally). A
	/// BREAK's drops ride the same message — the break and its drops get one
	/// arbitration verdict. MetalBonus rides raw: the receiver applies the
	/// game's own metallic multiplier to its identical generated block.
	/// </summary>
	public void SendBlockDamaged(NetVector2 worldPos, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new BlockDamagedMsg
		{
			Position = worldPos.ToNetVector2Msg(),
			Damage = damage,
			MetalBonus = metalBonus,
			Drops = drops is { Count: > 0 } ? [.. drops] : null,
		};
		if (_session.Role == SessionRole.Host)
		{
			_session.Broadcast(NetMsg.BlockDamaged, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.BlockDamaged, msg);
		}
	}

	/// <summary>
	/// Host only: relay an ACCEPTED guest break report (damage + drops) to the
	/// other members — the source is excluded, it already applied locally; the
	/// host's own application happened before this call. The host applies the
	/// guest's damage and drops itself via the same receive path (its own
	/// verdict: the break record it took when applying the guest's BlockPlaced).
	/// </summary>
	public void BroadcastBlockDamaged(ulong excludeSteamId, NetVector2 worldPos, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		var msg = new BlockDamagedMsg
		{
			Position = worldPos.ToNetVector2Msg(),
			Damage = damage,
			MetalBonus = metalBonus,
			Drops = drops is { Count: > 0 } ? [.. drops] : null,
		};
		_session.BroadcastExcept(excludeSteamId, NetMsg.BlockDamaged, msg);
	}
}
