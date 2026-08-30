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
internal sealed class EntityEventSync(IWorldControl world, ISessionControl session, TrapEffectApplier applier, TrapVisualReplay replay, WorldEntityKernelProjection kernelProjection, ILogger<EntityEventSync> log)
{
	private readonly IWorldControl _world = world;
	private readonly ISessionControl _session = session;
	private readonly TrapEffectApplier _applier = applier;
	private readonly TrapVisualReplay _replay = replay;
	private readonly WorldEntityKernelProjection _kernelProjection = kernelProjection;
	private readonly ILogger<EntityEventSync> _log = log;

	internal void BindToSession()
	{
		_world.EntityEventReceived += OnRemoteEntityEvent;
		_kernelProjection.TrapSnapshotProjected += OnTrapStateProjected;
	}

	internal void Unbind()
	{
		_world.EntityEventReceived -= OnRemoteEntityEvent;
		_kernelProjection.TrapSnapshotProjected -= OnTrapStateProjected;
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
		}, _session.Role == SessionRole.Host ? ReadDestroyedTrapHealth(kind, position) : null);
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

			// Record the whole trigger as one atomic kernel batch: one-shot
			// consumptions are position-keyed for the late-joiner snapshot,
			// and stateful edges move the kernel trap state machine for both
			// host-local and guest-reported events. For destructive trap
			// kinds the entity's post-trigger zero health also rides the same
			// batch. Repeatable visual-only events carry no kernel fact.
			var health = ReadDestroyedTrapHealth(msg.Kind, new Vector2(pos.X, pos.Y));
			_world.ReportTrapEvent(msg.Kind, pos.X, pos.Y, msg.Extra, health);
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

	private void OnTrapStateProjected(IReadOnlyList<EntityEventMsg> consumed)
	{
		// Late joiner: consume every one-shot consumption against the local
		// deterministic world — the entity is found by its position key (the
		// regenerated world has the identical entities), its state machine runs
		// (the consumption markers make the replay idempotent — a duplicate
		// entry is dropped by the per-entity guard). RemoteApply: the replays
		// must never re-report (the trap patches check the origin).
		_log.LogInformation("[TrapCheckpoint] projected {Count} consumed.", consumed.Count);
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			foreach (var msg in consumed)
			{
				var pos = msg.Position.ToNetVector2();
				_replay.Replay(msg.Kind, new Vector2(pos.X, pos.Y), msg.Extra, msg.ElapsedSeconds);
			}
		}
	}

	/// <summary>
	/// Returns the destroyed trap entity's post-trigger building health for the
	/// destructive trap kinds whose host application writes health = 0. Other
	/// kinds return null because they do not have a deterministic health fact to
	/// fold into the atomic trigger batch.
	/// </summary>
	private float? ReadDestroyedTrapHealth(EntityEventKind kind, Vector2 position)
	{
		if (!IsDestructiveTrapKind(kind))
		{
			return null;
		}

		var hit = Physics2D.OverlapPoint(position);
		var building = hit != null ? hit.GetComponent<BuildingEntity>() : null; // Unity object — ==
		return building != null && building.health <= 0.5f ? building.health : null; // Unity object — ==
	}

	private static bool IsDestructiveTrapKind(EntityEventKind kind) => kind is
		EntityEventKind.MineExploded
		or EntityEventKind.TurretSelfDestructed
		or EntityEventKind.CrystalFragileBroken
		or EntityEventKind.CrystalUnstableExploded;
}
