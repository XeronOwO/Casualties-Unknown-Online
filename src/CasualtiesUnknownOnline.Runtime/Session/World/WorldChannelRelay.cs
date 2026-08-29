using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The IWorldControl channel-forwarding surface. It shuttles world entity
/// events, trap consumptions, fluid, trader, speech and chat calls/events to
/// their dedicated channel classes. The world-defining state and message-flow
/// logic live in <see cref="WorldStateMessageService"/>; this class is only the
/// thin channel relay.
/// </summary>
internal sealed class WorldChannelRelay(
	EntityEventChannel eventChannel,
	TradeChannel tradeChannel,
	SpeechChannel speechChannel,
	ChatChannel chatChannel)
{
	private readonly EntityEventChannel _eventChannel = eventChannel;
	private readonly TradeChannel _tradeChannel = tradeChannel;
	private readonly SpeechChannel _speechChannel = speechChannel;
	private readonly ChatChannel _chatChannel = chatChannel;

	public event Action<ulong, EntityEventMsg>? EntityEventReceived { add => _eventChannel.EntityEventReceived += value; remove => _eventChannel.EntityEventReceived -= value; }

	public void FireEntityEventReceived(ulong sender, EntityEventMsg msg) => _eventChannel.FireEntityEventReceived(sender, msg);

	public void SendEntityEvent(EntityEventMsg msg) => _eventChannel.SendEntityEvent(msg);

	public void BroadcastEntityEvent(ulong excludeSteamId, EntityEventMsg msg) => _eventChannel.BroadcastEntityEvent(excludeSteamId, msg);

	public void SendDynamiteExplosion(ulong itemInstanceId, NetVector2 position) => _eventChannel.SendDynamiteExplosion(itemInstanceId, position);

	public void BroadcastDynamiteExplosion(ulong excludeSteamId, ulong itemInstanceId, NetVector2 position) => _eventChannel.BroadcastDynamiteExplosion(excludeSteamId, itemInstanceId, position);

	public event Action<ulong, ulong, NetVector2>? DynamiteExplosionReceived { add => _eventChannel.DynamiteExplosionReceived += value; remove => _eventChannel.DynamiteExplosionReceived -= value; }

	public void FireDynamiteExplosionReceived(ulong sender, ulong itemInstanceId, NetVector2 position) => _eventChannel.FireDynamiteExplosionReceived(sender, itemInstanceId, position);

	public void SendWorldBloodSpawn(WorldBloodSpawnMsg msg) => _eventChannel.SendWorldBloodSpawn(msg);

	public void BroadcastWorldBloodSpawn(ulong excludeSteamId, WorldBloodSpawnMsg msg) => _eventChannel.BroadcastWorldBloodSpawn(excludeSteamId, msg);

	public event Action<ulong, WorldBloodSpawnMsg>? WorldBloodSpawnReceived { add => _eventChannel.WorldBloodSpawnReceived += value; remove => _eventChannel.WorldBloodSpawnReceived -= value; }

	public void FireWorldBloodSpawnReceived(ulong sender, WorldBloodSpawnMsg msg) => _eventChannel.FireWorldBloodSpawnReceived(sender, msg);

	public void ReportTrapConsumed(EntityEventKind kind, float x, float y, byte extra) => _eventChannel.ReportTrapConsumed(kind, x, y, extra);

	public event Action<ulong, EntitySpawnedMsg>? EntitySpawnedReceived { add => _eventChannel.EntitySpawnedReceived += value; remove => _eventChannel.EntitySpawnedReceived -= value; }

	public void FireEntitySpawnedReceived(ulong sender, EntitySpawnedMsg msg) => _eventChannel.FireEntitySpawnedReceived(sender, msg);

	public void SendEntitySpawned(EntitySpawnedMsg msg) => _eventChannel.SendEntitySpawned(msg);

	public void BroadcastEntitySpawned(ulong excludeSteamId, EntitySpawnedMsg msg) => _eventChannel.BroadcastEntitySpawned(excludeSteamId, msg);

	public void ReportOpenedEntity(float x, float y) => _eventChannel.ReportOpenedEntity(x, y);

	public void ReportBuildingEntityHealth(float x, float y, float health) => _eventChannel.ReportBuildingEntityHealth(x, y, health);

	public void ReportTrapLayout(EntityEventKind kind, float x, float y, string prefabName) => _eventChannel.ReportTrapLayout(kind, x, y, prefabName);

	public void SendTrapLayoutSnapshot(ulong targetSteamId) => _eventChannel.SendTrapLayoutSnapshot(targetSteamId);

	public event Action<IReadOnlyList<TrapLayoutEntryMsg>>? TrapLayoutReceived { add => _eventChannel.TrapLayoutReceived += value; remove => _eventChannel.TrapLayoutReceived -= value; }

	public void FireTrapLayoutReceived(IReadOnlyList<TrapLayoutEntryMsg> entries) => _eventChannel.FireTrapLayoutReceived(entries);

	public void SendFluidRegion(ulong targetSteamId, FluidRegionMsg msg) => _eventChannel.SendFluidRegion(targetSteamId, msg);

	public event Action<FluidRegionMsg>? FluidRegionReceived { add => _eventChannel.FluidRegionReceived += value; remove => _eventChannel.FluidRegionReceived -= value; }

	public void FireFluidRegionReceived(FluidRegionMsg msg) => _eventChannel.FireFluidRegionReceived(msg);

	public void SendFluidInteraction(FluidInteractionMsg msg) => _eventChannel.SendFluidInteraction(msg);

	public void BroadcastFluidInteraction(ulong excludeSteamId, FluidInteractionMsg msg) => _eventChannel.BroadcastFluidInteraction(excludeSteamId, msg);

	public event Action<ulong, FluidInteractionMsg>? FluidInteractionReceived { add => _eventChannel.FluidInteractionReceived += value; remove => _eventChannel.FluidInteractionReceived -= value; }

	public void FireFluidInteractionReceived(ulong sender, FluidInteractionMsg msg) => _eventChannel.FireFluidInteractionReceived(sender, msg);

	public void SendFluidPresentation(ulong targetSteamId, FluidPresentationMsg msg) => _eventChannel.SendFluidPresentation(targetSteamId, msg);

	public event Action<FluidPresentationMsg>? FluidPresentationReceived { add => _eventChannel.FluidPresentationReceived += value; remove => _eventChannel.FluidPresentationReceived -= value; }

	public void FireFluidPresentationReceived(FluidPresentationMsg msg) => _eventChannel.FireFluidPresentationReceived(msg);

	public void SendTraderState(ulong targetSteamId, TraderStateMsg msg) => _tradeChannel.SendTraderState(targetSteamId, msg);

	public void BroadcastTraderState(TraderStateMsg msg) => _tradeChannel.BroadcastTraderState(msg);

	public event Action<TraderStateMsg>? TraderStateReceived { add => _tradeChannel.TraderStateReceived += value; remove => _tradeChannel.TraderStateReceived -= value; }

	public void FireTraderStateReceived(TraderStateMsg msg) => _tradeChannel.FireTraderStateReceived(msg);

	public void SendTraderAction(TraderActionMsg msg) => _tradeChannel.SendTraderAction(msg);

	public event Action<ulong, TraderActionMsg>? TraderActionReceived { add => _tradeChannel.TraderActionReceived += value; remove => _tradeChannel.TraderActionReceived -= value; }

	public void FireTraderActionReceived(ulong sender, TraderActionMsg msg) => _tradeChannel.FireTraderActionReceived(sender, msg);

	public void SendTraderRecruitRequest(TraderRecruitRequestMsg msg) => _tradeChannel.SendTraderRecruitRequest(msg);

	public event Action<ulong, TraderRecruitRequestMsg>? TraderRecruitRequestReceived { add => _tradeChannel.TraderRecruitRequestReceived += value; remove => _tradeChannel.TraderRecruitRequestReceived -= value; }

	public void FireTraderRecruitRequestReceived(ulong sender, TraderRecruitRequestMsg msg) => _tradeChannel.FireTraderRecruitRequestReceived(sender, msg);

	public void SendTraderRecruitResult(ulong targetSteamId, TraderRecruitResultMsg msg) => _tradeChannel.SendTraderRecruitResult(targetSteamId, msg);

	public event Action<TraderRecruitResultMsg>? TraderRecruitResultReceived { add => _tradeChannel.TraderRecruitResultReceived += value; remove => _tradeChannel.TraderRecruitResultReceived -= value; }

	public void FireTraderRecruitResultReceived(TraderRecruitResultMsg msg) => _tradeChannel.FireTraderRecruitResultReceived(msg);

	public void SendTraderSwing(TraderSwingMsg msg) => _tradeChannel.SendTraderSwing(msg);

	public event Action<ulong, TraderSwingMsg>? TraderSwingReceived { add => _tradeChannel.TraderSwingReceived += value; remove => _tradeChannel.TraderSwingReceived -= value; }

	public void FireTraderSwingReceived(ulong sender, TraderSwingMsg msg) => _tradeChannel.FireTraderSwingReceived(sender, msg);

	public void SendSpeech(SpeechMsg msg) => _speechChannel.SendSpeech(msg);

	public void BroadcastSpeech(ulong excludeSteamId, SpeechMsg msg) => _speechChannel.BroadcastSpeech(excludeSteamId, msg);

	public event Action<ulong, SpeechMsg>? SpeechReceived { add => _speechChannel.SpeechReceived += value; remove => _speechChannel.SpeechReceived -= value; }

	public void FireSpeechReceived(ulong sender, SpeechMsg msg) => _speechChannel.FireSpeechReceived(sender, msg);

	public void SendChat(ChatMsg msg) => _chatChannel.SendChat(msg);

	public void BroadcastChat(ulong excludeSteamId, ChatMsg msg) => _chatChannel.BroadcastChat(excludeSteamId, msg);

	public event Action<ulong, ChatMsg>? ChatReceived { add => _chatChannel.ChatReceived += value; remove => _chatChannel.ChatReceived -= value; }

	public void FireChatReceived(ulong sender, ChatMsg msg) => _chatChannel.FireChatReceived(sender, msg);
}
