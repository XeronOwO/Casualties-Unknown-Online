using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world entity channel: the entity-event (trap/mechanism) and
/// entity-creation (runtime spawn) message plumbing plus the one-shot trap
/// consumption registry — extracted from WorldService (the 600-line gate) so
/// the world domain stays one responsibility: WorldService owns the
/// world-defining state, this owns the entity channels. Same star shape as
/// everything else: report up (guest → host), the host applies to its own
/// world and relays (BroadcastExcept — the source already applied locally).
/// </summary>
public sealed class EntityEventChannel(ISessionControl session, PacketSender sender, TrapConsumptionRegistry trapConsumption, OpenedEntityRegistry openedEntities, BuildingEntityHealthRegistry buildingEntityHealth, TrapLayoutRegistry trapLayout)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly TrapConsumptionRegistry _trapConsumption = trapConsumption;
	private readonly OpenedEntityRegistry _openedEntities = openedEntities;
	private readonly BuildingEntityHealthRegistry _buildingEntityHealth = buildingEntityHealth;
	private readonly TrapLayoutRegistry _trapLayout = trapLayout;

	// ---- World entity events (traps/mechanisms) ----

	/// <summary>An entity event arrived — the receiver applies it (host: to its own world; guest: replay).</summary>
	public event Action<ulong, EntityEventMsg>? EntityEventReceived;

	public void FireEntityEventReceived(ulong sender, EntityEventMsg msg) => EntityEventReceived?.Invoke(sender, msg);

	/// <summary>
	/// Report a locally-triggered entity event (a trap fired on this side, local
	/// compute): guest → host as a report (the host applies the event to its own
	/// world — the mine destroys the host's copy and rolls the host-side drops —
	/// and relays), host → broadcast to all synced members. Same shape as
	/// SendBlockDamaged: the host owns the world consequences.
	/// </summary>
	public void SendEntityEvent(EntityEventMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			// The host already applied the trigger locally. Record a ONE-SHOT
			// consumption HERE: the host is not in its own presence table, so
			// its broadcast never comes back through EntityEventHandler, and
			// the remote-report path only records guest-triggered events.
			// Without this a late joiner never learns host-triggered
			// consumptions (mine, spike, mimic, ...).
			if (EntityEventProfiles.IsOneShotConsumption(msg.Kind))
			{
				_trapConsumption.Report(msg.Kind, msg.Position.X, msg.Position.Y, msg.Extra);
			}

			_session.Broadcast(NetMsg.EntityEvent, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.EntityEvent, msg);
		}
	}

	/// <summary>Host only: relay an accepted entity event to the other members (source excluded — it already applied locally).</summary>
	public void BroadcastEntityEvent(ulong excludeSteamId, EntityEventMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_session.BroadcastExcept(excludeSteamId, NetMsg.EntityEvent, msg);
	}

	// ---- World entity creation (runtime, outside generation) ----

	/// <summary>An entity-creation report arrived — the receiver creates its own copy (host: then relays; guest: remote apply).</summary>
	public event Action<ulong, EntitySpawnedMsg>? EntitySpawnedReceived;

	public void FireEntitySpawnedReceived(ulong sender, EntitySpawnedMsg msg) => EntitySpawnedReceived?.Invoke(sender, msg);

	/// <summary>
	/// Report a runtime world-entity creation (outside generation — the spawn
	/// command): guest → host as a report (the host creates its own copy and
	/// relays), host → broadcast to all synced members. Same shape as
	/// SendEntityEvent: the creating side keeps its local copy.
	/// </summary>
	public void SendEntitySpawned(EntitySpawnedMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			_session.Broadcast(NetMsg.EntitySpawned, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.EntitySpawned, msg);
		}
	}

	/// <summary>Host only: relay an accepted entity creation to the other members (source excluded — it already created locally).</summary>
	public void BroadcastEntitySpawned(ulong excludeSteamId, EntitySpawnedMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_session.BroadcastExcept(excludeSteamId, NetMsg.EntitySpawned, msg);
	}

	// ---- World fluid grid (host authority, #129) ----

	/// <summary>Host only: stream an absolute RLE fluid-grid region to one member (the host simulates the world fluid alone; the guest applies it onto its local grid and the game's own renderer draws it). Unreliable: every region is an absolute overwrite, so a lost one is healed by the next (a reliable stream would queue the newest snapshot behind retransmissions — head-of-line blocking).</summary>
	public void SendFluidRegion(ulong targetSteamId, FluidRegionMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.FluidRegion, msg, reliable: false);
	}

	/// <summary>Guest: the host's fluid region arrived — apply it onto the local grid.</summary>
	public event Action<FluidRegionMsg>? FluidRegionReceived;

	public void FireFluidRegionReceived(FluidRegionMsg msg) => FluidRegionReceived?.Invoke(msg);

	/// <summary>
	/// Report a locally-performed fluid interaction (drinking — the cell was
	/// consumed): guest → host as a report (the host executes on its own grid —
	/// the authority — and relays), host → broadcast to all synced members.
	/// Same shape as SendEntityEvent.
	/// </summary>
	public void SendFluidInteraction(FluidInteractionMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			_session.Broadcast(NetMsg.FluidInteraction, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.FluidInteraction, msg);
		}
	}

	/// <summary>Host only: relay an executed fluid interaction to the other members (source excluded — it already applied locally).</summary>
	public void BroadcastFluidInteraction(ulong excludeSteamId, FluidInteractionMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_session.BroadcastExcept(excludeSteamId, NetMsg.FluidInteraction, msg);
	}

	/// <summary>A fluid interaction arrived — the receiver applies it (host: to its own grid, then relays; guest: clear the cell).</summary>
	public event Action<ulong, FluidInteractionMsg>? FluidInteractionReceived;

	public void FireFluidInteractionReceived(ulong sender, FluidInteractionMsg msg) => FluidInteractionReceived?.Invoke(sender, msg);

	// ---- One-shot trap consumptions (the late-joiner snapshot) ----

	/// <summary>Host only: record a one-shot trap consumption (position-keyed; Extra rides along for progress-carrying events).</summary>
	public void ReportTrapConsumed(EntityEventKind kind, float x, float y, byte extra) => _trapConsumption.Report(kind, x, y, extra);

	/// <summary>Host only: send the one-shot trap consumptions to one member (on its world entry).</summary>
	public void SendTrapStateSnapshot(ulong targetSteamId) => _trapConsumption.SendSnapshot(targetSteamId);

	/// <summary>Host only: a new world layer is generating — the consumptions start empty again.</summary>
	public void ResetConsumptions() => _trapConsumption.Reset();

	/// <summary>Guest: the host's trap-consumption snapshot arrived — consume each entry (idempotent).</summary>
	public event Action<IReadOnlyList<EntityEventMsg>>? TrapStateReceived;

	public void FireTrapStateReceived(IReadOnlyList<EntityEventMsg> consumed) => TrapStateReceived?.Invoke(consumed);

	// ---- Opened lockable entities (the late-joiner snapshot) ----

	/// <summary>Host only: record an opened entity at a world position.</summary>
	public void ReportOpenedEntity(float x, float y) => _openedEntities.Report(x, y);

	/// <summary>Host only: send the opened positions to one member (on its world entry).</summary>
	public void SendOpenedEntitiesSnapshot(ulong targetSteamId) => _openedEntities.SendSnapshot(targetSteamId);

	/// <summary>Host only: a new world layer is generating — the opens start empty again.</summary>
	public void ResetOpenedEntities() => _openedEntities.Reset();

	/// <summary>Guest: the host's opened-entities snapshot arrived — apply each open (idempotent).</summary>
	public event Action<IReadOnlyList<NetVector2Msg>>? OpenedEntitiesSnapshotReceived;

	public void FireOpenedEntitiesSnapshotReceived(IReadOnlyList<NetVector2Msg> positions) => OpenedEntitiesSnapshotReceived?.Invoke(positions);

	// ---- Damaged building entities (the late-joiner health snapshot) ----

	/// <summary>Host only: record a damaged building entity's current health at a world position.</summary>
	public void ReportBuildingEntityHealth(float x, float y, float health) => _buildingEntityHealth.Report(x, y, health);

	/// <summary>Host only: send the recorded entity health to one member (on its world entry).</summary>
	public void SendBuildingEntityHealthSnapshot(ulong targetSteamId) => _buildingEntityHealth.SendSnapshot(targetSteamId);

	/// <summary>Host only: a new world layer is generating — the health records start empty again.</summary>
	public void ResetBuildingEntityHealth() => _buildingEntityHealth.Reset();

	/// <summary>Guest: the host's building-entity health snapshot arrived — apply each entry (idempotent).</summary>
	public event Action<IReadOnlyList<BuildingEntityHealthEntryMsg>>? BuildingEntityHealthSnapshotReceived;

	public void FireBuildingEntityHealthSnapshotReceived(IReadOnlyList<BuildingEntityHealthEntryMsg> entries) => BuildingEntityHealthSnapshotReceived?.Invoke(entries);

	// ---- Trap layout (host authority — the generated trap entities' positions) ----

	/// <summary>Host only: record one generated trap entity (the adapter's scanner reports it on the generation-finished edge).</summary>
	public void ReportTrapLayout(EntityEventKind kind, float x, float y, string prefabName) => _trapLayout.Report(kind, x, y, prefabName);

	/// <summary>Host only: send the layout to one member (on its world entry).</summary>
	public void SendTrapLayoutSnapshot(ulong targetSteamId) => _trapLayout.SendSnapshot(targetSteamId);

	/// <summary>Host only: a new world layer is generating — the layout starts empty again.</summary>
	public void ResetTrapLayouts() => _trapLayout.Reset();

	/// <summary>Guest: the host's trap layout arrived — align the local world (materialize missing, destroy surplus).</summary>
	public event Action<IReadOnlyList<TrapLayoutEntryMsg>>? TrapLayoutReceived;

	public void FireTrapLayoutReceived(IReadOnlyList<TrapLayoutEntryMsg> entries) => TrapLayoutReceived?.Invoke(entries);
}
