using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The entity-event domain (traps/mechanisms, #123): ONE coordinator owning the
/// trigger → report → host-apply → relay → replay chain. The Harmony trap
/// patches are thin adapters — they observe a trap's own trigger transition
/// and call the bridge's OnTrapTriggered, never any domain logic. Events are
/// position-keyed: world entities are generated deterministically on both
/// sides, so the trap's transform position IS its identity (same pattern as
/// the building-entity damage/open events).
/// </summary>
internal sealed class EntityEventSync(IWorldControl world, ISessionControl session, ILogger<EntityEventSync> log)
{
	private readonly IWorldControl _world = world;
	private readonly ISessionControl _session = session;
	private readonly ILogger<EntityEventSync> _log = log;

	internal void BindToSession()
	{
		_world.EntityEventReceived += OnRemoteEntityEvent;
		_world.TrapStateReceived += OnTrapStateReceived;
	}

	internal void Unbind()
	{
		_world.EntityEventReceived -= OnRemoteEntityEvent;
		_world.TrapStateReceived -= OnTrapStateReceived;
	}

	/// <summary>
	/// Patch-bridge entry: a trap fired locally (the patch verified the trigger
	/// transition). The full local effect already ran (original game behaviour —
	/// local compute); the event now travels: guest → host report (the host
	/// applies it to its own world and relays), host → broadcast. A RemoteApply
	/// replay must never re-report.
	/// </summary>
	internal void OnTrapTriggered(EntityEventKind kind, Vector2 position, byte extra)
	{
		if (CallContext.Current == CallContext.Origin.RemoteApply || !_session.SessionActive)
		{
			return;
		}

		_log.LogInformation("[TrapEvent] kind={Kind} pos=({X:F1},{Y:F1}) origin={Origin}.",
			kind, position.x, position.y, _session.Role == SessionRole.Host ? "HostApply" : "Report");
		_world.SendEntityEvent(new EntityEventMsg
		{
			Kind = kind,
			Position = new NetVector2Msg(position.x, position.y),
			Extra = extra,
		});
	}

	private void OnRemoteEntityEvent(ulong sender, EntityEventMsg msg)
	{
		var pos = msg.Position.ToNetVector2();
		if (_session.Role == SessionRole.Host)
		{
			// The host applies the event to its own world first (the
			// TrapEffectApplier — destroy the host's copy, roll its drops,
			// explode, diff the building damage) and relays afterwards.
			_log.LogInformation("[TrapEvent] kind={Kind} pos=({X:F1},{Y:F1}) origin=HostApply from {Sender}.",
				msg.Kind, pos.X, pos.Y, sender);
			_world.ReportTrapConsumed(msg.Kind, pos.X, pos.Y); // one-shot consumptions, position-keyed (the late-joiner snapshot)
			_world.BroadcastEntityEvent(sender, msg);
		}
		else
		{
			// The host's relay — replay the event (pure visual + real-body
			// effects + entity state). The replay registry lands with the
			// TrapVisualReplay step; until then this log line is the trace.
			_log.LogInformation("[TrapEvent] kind={Kind} pos=({X:F1},{Y:F1}) origin=Replay.", msg.Kind, pos.X, pos.Y);
		}
	}

	private void OnTrapStateReceived(IReadOnlyList<EntityEventMsg> consumed) =>
		// Late joiner: consume every one-shot consumption against the local
		// deterministic world (idempotent). Wired with the snapshot step.
		_log.LogInformation("[TrapSnapshot] received {Count} consumed.", consumed.Count);
}
