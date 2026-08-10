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
internal sealed class EntityEventSync(IWorldControl world, ISessionControl session, TrapEffectApplier applier, TrapVisualReplay replay, ILogger<EntityEventSync> log)
{
	private readonly IWorldControl _world = world;
	private readonly ISessionControl _session = session;
	private readonly TrapEffectApplier _applier = applier;
	private readonly TrapVisualReplay _replay = replay;
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
			// TrapEffectApplier — destroy the host's copy, explode with the
			// trap's parameters; the building damage rides the CreateExplosion
			// diff) and relays afterwards. NOT inside RemoteApply: the host's
			// consequences must flow out (the crater rides the SetBlock relay).
			_log.LogInformation("[TrapEvent] kind={Kind} pos=({X:F1},{Y:F1}) origin=HostApply from {Sender}.",
				msg.Kind, pos.X, pos.Y, sender);
			_applier.ApplyEvent(msg.Kind, new Vector2(pos.X, pos.Y), msg.Extra);
			if (IsOneShotConsumption(msg.Kind))
			{
				// One-shot consumptions are position-keyed for the late-joiner
				// snapshot. Repeatable events (clamps, fences, heat toggles, ...)
				// are NOT recorded — each side's copy re-arms naturally.
				_world.ReportTrapConsumed(msg.Kind, pos.X, pos.Y, msg.Extra);
			}
			_world.BroadcastEntityEvent(sender, msg);
		}
		else
		{
			// The host's relay — replay the event: pure visual + real-body
			// effects + entity consumption. RemoteApply: the replay must never
			// re-report (the trap patches check the origin).
			using (CallContext.Enter(CallContext.Origin.RemoteApply))
			{
				_log.LogInformation("[TrapEvent] kind={Kind} pos=({X:F1},{Y:F1}) origin=Replay.", msg.Kind, pos.X, pos.Y);
				_replay.Replay(msg.Kind, new Vector2(pos.X, pos.Y), msg.Extra);
			}
		}
	}

	/// <summary>One-shot consumptions land in the late-joiner snapshot; repeatable events do not (each side's copy re-arms naturally, the vanilla behaviour).</summary>
	private static bool IsOneShotConsumption(EntityEventKind kind) => kind switch
	{
		EntityEventKind.MineExploded or EntityEventKind.SpikeStabbed or EntityEventKind.StalactiteDropped
			or EntityEventKind.SoundCannonFired or EntityEventKind.TurretSelfDestructed
			or EntityEventKind.CrystalFragileBroken or EntityEventKind.CaveTicksSpawned
			or EntityEventKind.ShuttleDoorOpened or EntityEventKind.LifepodShowerActivated
			or EntityEventKind.BioTerminalUnlocked or EntityEventKind.MedStationHealed
			or EntityEventKind.ScrapEaterProgress or EntityEventKind.BatteryInserted
			=> true,
		_ => false,
	};

	private void OnTrapStateReceived(IReadOnlyList<EntityEventMsg> consumed) =>
		// Late joiner: consume every one-shot consumption against the local
		// deterministic world (idempotent). Wired with the snapshot step.
		_log.LogInformation("[TrapSnapshot] received {Count} consumed.", consumed.Count);
}
