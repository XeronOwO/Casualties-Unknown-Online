using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The building-entity part of the world-event domain: damage and open
/// reports (live star relay), the opened-entity snapshot and the
/// building-entity health snapshot. Split from WorldEventSync at the 600-line
/// gate — the block/keypad/earthquake domain stays in the main partial.
/// </summary>
internal sealed partial class WorldEventSync
{
	/// <summary>
	/// Called from the Body.Attack patch after the local attack damaged a
	/// building entity (Body.cs:1946 — the only player-vs-entity damage write,
	/// which otherwise stays local and the peer's copy of the entity never
	/// loses health): report it, position-keyed (world entities are generated
	/// deterministically, so both sides have the same object at the same place).
	/// </summary>
	internal void OnBuildingEntityDamaged(BuildingEntity entity, float damage, bool playHitSound = true)
	{
		if (IsRemoteApply || !_session.SessionActive)
		{
			return;
		}

		var pos = entity.transform.position;
		_world.SendBuildingEntityDamaged(new NetVector2(pos.x, pos.y), damage, playHitSound);
		_world.ReportBuildingEntityHealth(pos.x, pos.y, entity.health); // host-only — the late-joiner snapshot's fact source
		_trace.End(_trace.NextOperationId(), 0, "OnBuildingEntityDamaged", "Committed(1)", "EntityDamage");
	}

	/// <summary>
	/// A building entity was damaged — apply the damage to the entity at the
	/// reported position. A death applied HERE (via this message) is a REMOTE
	/// death: the attacker's side rolls and reports the drops (local compute —
	/// the entity's health is written on both sides, so both reach zero; only
	/// the attacker rolls), so this side is marked with RemoteEntityDeath and
	/// BuildingEntityUpdatePatch suppresses the roll — it only removes the
	/// entity. Attack damage replays the entity's own hitSound; silent damage
	/// sources (cactus collision self-damage) pass playHitSound = false because
	/// the trigger side never played the entity hitSound.
	/// </summary>
	private void OnRemoteBuildingEntityDamaged(NetVector2 pos, float damage, bool playHitSound)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var hit = Physics2D.OverlapPoint(new Vector2(pos.X, pos.Y));
			var entity = hit != null ? hit.GetComponent<BuildingEntity>() : null; // Unity object — ==
			if (entity != null)
			{
				entity.health -= damage;
				// Attack damage: every side applying the relay plays the entity's own hitSound (the attacker heard it locally, Body.cs:1953). Silent damage sources skip it.
				if (playHitSound)
				{
					Sound.Play(entity.hitSound, entity.transform.position, false, true, null, 1f, 1f, false, false);
				}
				if (entity.health < 0.5f)
				{
					MarkRemoteEntityDeath(entity, replayAnimalDeath: true);
				}

				_world.ReportBuildingEntityHealth(pos.X, pos.Y, entity.health); // host-only — a guest-reported hit applied here is part of the authoritative history
			}
			else
			{
				_log.LogWarning("Building entity damage at {Pos} — no entity there (moved or already gone).", pos);
			}
		}
	}

	/// <summary>
	/// Called from the Openable/lockpick/keypad patches after a lockable entity
	/// was opened locally (all three paths write health = 0 directly) — report
	/// it, position-keyed like the entity damage.
	/// </summary>
	internal void OnBuildingEntityOpened(BuildingEntity entity)
	{
		if (IsRemoteApply || !_session.SessionActive)
		{
			return;
		}

		var pos = entity.transform.position;
		_world.SendBuildingEntityOpened(new NetVector2(pos.x, pos.y));
		_world.ReportBuildingEntityHealth(pos.x, pos.y, entity.health); // an open is health = 0 — the snapshot covers it too
		_trace.End(_trace.NextOperationId(), 0, "OnBuildingEntityOpened", "Committed(1)", "Open");
	}

	/// <summary>
	/// A lockable entity was opened — apply the open (health = 0) to the entity
	/// at the reported position. Like the damage path, a death applied here is
	/// REMOTE: the opener's side rolls and reports the drops, this side is
	/// marked and BuildingEntityUpdatePatch only removes the entity.
	/// </summary>
	private void OnRemoteBuildingEntityOpened(NetVector2 pos)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var hit = Physics2D.OverlapPoint(new Vector2(pos.X, pos.Y));
			var entity = hit != null ? hit.GetComponent<BuildingEntity>() : null; // Unity object — ==
			if (entity != null)
			{
				entity.health = 0f;
				MarkRemoteEntityDeath(entity, replayAnimalDeath: true);
				_world.ReportBuildingEntityHealth(pos.X, pos.Y, 0f); // host-only — the opened state is part of the late-joiner history
			}
			else
			{
				_log.LogWarning("Building entity open at {Pos} — no entity there (moved or already gone).", pos);
			}
		}
	}

	/// <summary>
	/// The host's opened-entities snapshot arrived (world entry / the 60 s
	/// resend) — apply every open through the SAME application as the live
	/// relay (health = 0 + the remote-death mark). Idempotent by construction:
	/// an already-open entity's health is 0 again.
	/// </summary>
	private void OnOpenedEntitiesSnapshot(IReadOnlyList<NetVector2Msg> positions)
	{
		foreach (var pos in positions)
		{
			OnRemoteBuildingEntityOpened(new NetVector2(pos.X, pos.Y));
		}

		_log.LogInformation("Opened-entities snapshot applied ({Count} positions).", positions.Count);
	}

	/// <summary>
	/// The host's building-entity health snapshot arrived (world entry / the
	/// 60 s resend) — apply every entry through the SAME semantic as the live
	/// relay: write the host's current health, and mark a death applied here as
	/// remote so this side never rolls a second set of drops. Idempotent by
	/// construction: writing the same health again is a no-op.
	/// </summary>
	private void OnBuildingEntityHealthSnapshot(IReadOnlyList<BuildingEntityHealthEntryMsg> entries)
	{
		foreach (var entry in entries)
		{
			ApplyRemoteBuildingEntityHealth(entry.X, entry.Y, entry.Health);
		}

		_log.LogInformation("Building-entity health snapshot applied ({Count} entities).", entries.Count);
	}

	private void ApplyRemoteBuildingEntityHealth(float x, float y, float health)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var hit = Physics2D.OverlapPoint(new Vector2(x, y));
			var entity = hit != null ? hit.GetComponent<BuildingEntity>() : null; // Unity object — ==
			if (entity != null)
			{
				entity.health = health;
				if (entity.health < 0.5f)
				{
					MarkRemoteEntityDeath(entity, replayAnimalDeath: false);
				}
			}
			else
			{
				_log.LogWarning("Building-entity health snapshot at ({X}, {Y}) — no entity there (moved or already gone).", x, y);
			}
		}
	}

	/// <summary>
	/// Adds (or reuses) the <see cref="RemoteEntityDeath"/> marker and records
	/// whether this death was a live remote event or a late-joiner snapshot
	/// application. The flag lets <see cref="BuildingEntityUpdatePatch"/> replay
	/// the animal-specific presentation only for a death the current world
	/// session actually observed, while snapshot-applied pre-existing deaths
	/// stay silent on the creature-specific effects.
	/// </summary>
	private static void MarkRemoteEntityDeath(BuildingEntity entity, bool replayAnimalDeath)
	{
		var death = entity.gameObject.GetComponent<RemoteEntityDeath>();
		if (death == null) // Unity object — ==
		{
			death = entity.gameObject.AddComponent<RemoteEntityDeath>();
		}

		// A snapshot application must never downgrade an already-marked live
		// remote death (a resend landed in the same window would otherwise
		// suppress the creature-specific replay).
		if (replayAnimalDeath)
		{
			death.ReplayAnimalDeath = true;
		}
	}
}
