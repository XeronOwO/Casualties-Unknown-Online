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
public sealed class EntityEventChannel(ISessionControl session, PacketSender sender, TrapConsumptionRegistry trapConsumption)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly TrapConsumptionRegistry _trapConsumption = trapConsumption;

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
}
