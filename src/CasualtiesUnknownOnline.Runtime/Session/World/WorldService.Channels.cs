using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The IWorldControl channel-forwarding members of <see cref="WorldService"/>
/// (split off at the 600-line gate — the world entity channels accumulated
/// five forwarding surfaces: events, spawns, trap consumptions, fluid, trade).
/// Each surface is one thin delegation to its channel class; the world-defining
/// state stays in WorldService.cs.
/// </summary>
public sealed partial class WorldService
{
	public event Action<ulong, EntityEventMsg>? EntityEventReceived { add => _eventChannel.EntityEventReceived += value; remove => _eventChannel.EntityEventReceived -= value; }

	public void FireEntityEventReceived(ulong sender, EntityEventMsg msg) => _eventChannel.FireEntityEventReceived(sender, msg);

	public void SendEntityEvent(EntityEventMsg msg) => _eventChannel.SendEntityEvent(msg);

	public void BroadcastEntityEvent(ulong excludeSteamId, EntityEventMsg msg) => _eventChannel.BroadcastEntityEvent(excludeSteamId, msg);

	public void ReportTrapConsumed(EntityEventKind kind, float x, float y, byte extra) => _eventChannel.ReportTrapConsumed(kind, x, y, extra);

	public event Action<ulong, EntitySpawnedMsg>? EntitySpawnedReceived { add => _eventChannel.EntitySpawnedReceived += value; remove => _eventChannel.EntitySpawnedReceived -= value; }

	public void FireEntitySpawnedReceived(ulong sender, EntitySpawnedMsg msg) => _eventChannel.FireEntitySpawnedReceived(sender, msg);

	public void SendEntitySpawned(EntitySpawnedMsg msg) => _eventChannel.SendEntitySpawned(msg);

	public void BroadcastEntitySpawned(ulong excludeSteamId, EntitySpawnedMsg msg) => _eventChannel.BroadcastEntitySpawned(excludeSteamId, msg);

	public void SendTrapStateSnapshot(ulong targetSteamId) => _eventChannel.SendTrapStateSnapshot(targetSteamId);

	public event Action<IReadOnlyList<EntityEventMsg>>? TrapStateReceived { add => _eventChannel.TrapStateReceived += value; remove => _eventChannel.TrapStateReceived -= value; }

	public void FireTrapStateReceived(IReadOnlyList<EntityEventMsg> consumed) => _eventChannel.FireTrapStateReceived(consumed);

	public void SendFluidRegion(ulong targetSteamId, FluidRegionMsg msg) => _eventChannel.SendFluidRegion(targetSteamId, msg);

	public event Action<FluidRegionMsg>? FluidRegionReceived { add => _eventChannel.FluidRegionReceived += value; remove => _eventChannel.FluidRegionReceived -= value; }

	public void FireFluidRegionReceived(FluidRegionMsg msg) => _eventChannel.FireFluidRegionReceived(msg);

	public void SendFluidInteraction(FluidInteractionMsg msg) => _eventChannel.SendFluidInteraction(msg);

	public void BroadcastFluidInteraction(ulong excludeSteamId, FluidInteractionMsg msg) => _eventChannel.BroadcastFluidInteraction(excludeSteamId, msg);

	public event Action<ulong, FluidInteractionMsg>? FluidInteractionReceived { add => _eventChannel.FluidInteractionReceived += value; remove => _eventChannel.FluidInteractionReceived -= value; }

	public void FireFluidInteractionReceived(ulong sender, FluidInteractionMsg msg) => _eventChannel.FireFluidInteractionReceived(sender, msg);

	public void SendTraderState(ulong targetSteamId, TraderStateMsg msg) => _tradeChannel.SendTraderState(targetSteamId, msg);

	public void BroadcastTraderState(TraderStateMsg msg) => _tradeChannel.BroadcastTraderState(msg);

	public event Action<TraderStateMsg>? TraderStateReceived { add => _tradeChannel.TraderStateReceived += value; remove => _tradeChannel.TraderStateReceived -= value; }

	public void FireTraderStateReceived(TraderStateMsg msg) => _tradeChannel.FireTraderStateReceived(msg);

	public void SendTraderAction(TraderActionMsg msg) => _tradeChannel.SendTraderAction(msg);

	public event Action<ulong, TraderActionMsg>? TraderActionReceived { add => _tradeChannel.TraderActionReceived += value; remove => _tradeChannel.TraderActionReceived -= value; }

	public void FireTraderActionReceived(ulong sender, TraderActionMsg msg) => _tradeChannel.FireTraderActionReceived(sender, msg);

	public void SendSpeech(SpeechMsg msg) => _speechChannel.SendSpeech(msg);

	public void BroadcastSpeech(ulong excludeSteamId, SpeechMsg msg) => _speechChannel.BroadcastSpeech(excludeSteamId, msg);

	public event Action<ulong, SpeechMsg>? SpeechReceived { add => _speechChannel.SpeechReceived += value; remove => _speechChannel.SpeechReceived -= value; }

	public void FireSpeechReceived(ulong sender, SpeechMsg msg) => _speechChannel.FireSpeechReceived(sender, msg);
}
