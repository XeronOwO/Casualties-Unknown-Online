using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.GameAdapter.Tutorial;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Runtime world-entity creation sync (the spawn command): a BuildingEntity
/// starting OUTSIDE world generation is a runtime creation — the creating side
/// keeps its local copy and reports (id + position + rotation + creation-time
/// initial data + the creation-instance token); the host creates its own copy,
/// generates the keypad code if the created entity is a keypad (its Random
/// stream is the authority) and relays to every member (the source included —
/// the relay is idempotent via the creation key, and the source's keypad code
/// may need the carried value); every receiving side creates the same entity at
/// the same place and applies the carried data. World-generation entities are
/// skipped: they are deterministic on both sides. Items do NOT ride this
/// channel.
///
/// Creation-time initial data (#128 — one message per operation, data is
/// decided at creation): a created geyser's liquid type rolls at the entity's
/// OWN Start (GeyserScript.cs:12 — the child's Start runs after the parent
/// BuildingEntity's, so at report time the type does not exist yet): the
/// creating side waits ONE frame, reads its copy's type and reports it IN the
/// EntitySpawnedMsg; every receiving side applies the carried value one frame
/// later — after its own copy's Start re-rolled it (GeyserScript.cs:12 writes
/// unconditionally). The creating side's value is the authority (it keeps its
/// own copy — the channel's existing semantics). A created KEYPAD's code is
/// generated host-side (host authority) and carried in the same message; the
/// receiver writes it immediately (the game's lazy generation skips an
/// already-set code, Openable.cs:19 — no Start wait needed for strings).
/// </summary>
internal sealed class EntitySpawnSync(IWorldControl world, ISessionControl session, ILogger<EntitySpawnSync> log)
{
	private readonly IWorldControl _world = world;
	private readonly ISessionControl _session = session;
	private readonly ILogger<EntitySpawnSync> _log = log;

	/// <summary>
	/// The creating side's monotonic creation-instance counter (the token's
	/// second half; 0 means "no token", so it never starts at 0). Seeded from a
	/// random value: the token must be unique across PROCESS lifetimes, not just
	/// within one — a creator that restarts re-issues sequence 1, and the host's
	/// accepted table survives that reconnect (it only resets at a layer
	/// boundary), so a fixed start could make a new creation collide with a
	/// record the host still holds.
	/// </summary>
	private uint _creationSequence = ((uint)Guid.NewGuid().GetHashCode() | 1u);

	/// <summary>Geyser creations awaiting their child Start (value tuples — no Unity references held).</summary>
	private readonly List<(RuntimeEntityKey Key, Vector2 Pos, float Rotation, int AtFrame, bool IsAnimal)> _reportQueue = [];

	/// <summary>Received geyser creations awaiting their own copy's Start before the carried type is applied.</summary>
	private readonly List<(RuntimeEntityKey Key, Vector2 Pos, byte Type, int AtFrame)> _applyQueue = [];

	internal void BindToSession() => _world.EntitySpawnedReceived += OnRemoteEntitySpawned;

	internal void Unbind() => _world.EntitySpawnedReceived -= OnRemoteEntitySpawned;

	internal void Update()
	{
		FlushReports();
		FlushApplies();
	}

	/// <summary>
	/// Create a runtime world-entity prefab on behalf of a mod and leave the
	/// normal <see cref="BuildingEntity"/> Start report to replicate it to the
	/// peers. Returns false (and destroys the created object when it is not a
	/// BuildingEntity) so a mod can never create a local-only orphan through
	/// this surface.
	/// </summary>
	internal bool TrySpawnFromMod(string prefabId, float x, float y, float rotation)
	{
		if (!_session.SessionActive || string.IsNullOrEmpty(prefabId))
		{
			return false;
		}

		var created = RuntimeEntityFactory.TryCreate(prefabId, new Vector2(x, y), _log, "EntitySpawn.mod");
		if (created == null) // Unity object — ==
		{
			return false;
		}

		created.transform.eulerAngles = new Vector3(0f, 0f, rotation);
		_log.LogInformation("[EntitySpawn] mod-requested {Id} created at ({X:F1},{Y:F1}); the Start report will replicate it.", prefabId, x, y);
		return true;
	}

	/// <summary>
	/// Patch-bridge entry: a BuildingEntity's health reached the death
	/// threshold (the single death funnel in BuildingEntity.Update). The world
	/// domain drops the creation's accepted record (host) or unacknowledged
	/// report (guest) so the runtime-entity backfill never re-materializes a
	/// dead entity. Generated entities carry no marker — they never entered
	/// either table, so this is a no-op for them.
	/// </summary>
	internal void OnRuntimeEntityDestroyed(BuildingEntity entity)
	{
		// The record key is the CREATION identity, and a BuildingEntity's
		// Rigidbody2D becomes Dynamic while its chunk is visible
		// (BuildingEntity.cs:54), so a runtime creation can drift before it
		// dies. Report the stamped creation key, never transform.position —
		// otherwise the record would never be dropped and the 60 s re-broadcast
		// would resurrect the dead entity. The token half keeps the death of one
		// of two same-cell siblings from dropping the other's record.
		if (RuntimeEntityCreation.TryRead(entity, out var key))
		{
			_world.ReportRuntimeEntityDestroyed(key);
		}
	}

	/// <summary>
	/// Patch-bridge entry: a world entity just started. Inside world generation
	/// = deterministic (both sides generate the same entity — nothing to do); a
	/// RemoteApply create = a replay of this very channel (nothing to do);
	/// anything else in a session = a runtime creation — report it with a fresh
	/// creation-instance token. A geyser's report is deferred ONE frame: its
	/// liquid type rolls at the CHILD's Start (GeyserScript.cs:12), which runs
	/// after this parent Start — the creation message carries the initial data
	/// once it exists. A created keypad's code is generated HOST-side now (the
	/// host's Random stream is the authority).
	/// </summary>
	internal void OnEntityInstantiated(BuildingEntity entity)
	{
		// A tutorial-claw prop is per-player course state: each side's
		// TutorialHandler creates its own copy (TutorialHandler.cs:255-271)
		// and reporting both copies made every prop appear twice on both
		// sides (the claw double-give, entity branch). It never enters the
		// shared entity domain.
		if (entity.GetComponent<TutorialClawProp>() != null) // Unity object — ==
		{
			_log.LogInformation("[TutorialClaw] {Id} left as a per-player course prop (no spawn report).", entity.id);
			return;
		}

		if (entity.GetComponent<SpawnReplayMarker>() != null) // Unity object — ==; a replay of this channel must not re-report
		{
			return;
		}

		if (CallContext.Current == CallContext.Origin.RemoteApply || !_session.SessionActive || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		if (string.IsNullOrEmpty(entity.id))
		{
			return; // no prefab id — nothing to recreate on the peers
		}

		var pos = entity.transform.position;
		var key = NextCreationKey(entity.id, pos.x, pos.y);
		RuntimeEntityCreation.Stamp(entity, key); // the creation key the death hook reports later (the entity may drift)
		if (entity.GetComponentInChildren<GeyserScript>() != null) // Unity object — ==
		{
			// The deferred report must carry THIS creation key and position: the
			// geyser child's own transform can sit in another cell than the root
			// the marker was stamped from, and re-deriving the record from it
			// made the host's record key differ from the death key — the record
			// then never dropped and the 60 s re-broadcast resurrected a
			// destroyed geyser.
			_reportQueue.Add((key, pos, entity.transform.eulerAngles.z, Time.frameCount, entity.animal));
			return;
		}

		var openable = entity.GetComponent<Openable>();
		var keypadCode = openable is not null && openable.isKeypad && _session.Role == SessionRole.Host
			? WorldEventSync.EnsureKeypadCode(openable) // the host creates it — its code is host authority from the start
			: "";
		TryCaptureEnemyTint(entity, out var hasTint, out var tint, out var lightIntensity);
		ReportSpawn(key, pos, entity.transform.eulerAngles.z, 0, keypadCode, hasTint, tint, lightIntensity, entity.animal);
	}

	/// <summary>
	/// A runtime-created crystalenemy's trigger-side SetColor (CrystalMimic.cs:32/46)
	/// ran BEFORE its BuildingEntity.Start — SetColor is synchronous inside the
	/// touched/hit callback, Start is deferred to the frame's start phase — so at
	/// this report the copy already carries its final tint: capture it as creation
	/// data so every receiving side paints its own copy with the EXACT color (never
	/// a re-roll of the per-side-random SetColor jitter, CrystalEnemy.cs:210-212).
	/// </summary>
	private static void TryCaptureEnemyTint(BuildingEntity entity, out bool hasTint, out NetColorRgba tint, out float lightIntensity)
	{
		var crystal = entity.GetComponentInChildren<CrystalEnemy>();
		if (crystal != null && CrystalEnemyTintAccess.TryRead(crystal, out var color, out lightIntensity)) // Unity object — ==
		{
			hasTint = true;
			tint = new NetColorRgba(color.r, color.g, color.b, color.a);
			return;
		}

		hasTint = false;
		tint = default;
		lightIntensity = 0f;
	}

	private void OnRemoteEntitySpawned(ulong sender, EntitySpawnedMsg msg)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var pos = new Vector2(msg.Position.X, msg.Position.Y);
			var key = RuntimeEntityKey.From(msg);
			var created = FindExisting(key, msg.Position.X, msg.Position.Y);
			if (created == null) // Unity object — ==
			{
				// Utils.Create calls Object.Instantiate directly for a vanilla id
				// and THROWS when the prefab is missing; a mod-registered
				// template is materialized by UtilsCreateCustomPrefabPatch
				// instead (Resources.Load returns nothing for it BY DESIGN), so
				// there must be NO Resources pre-check here.
				created = RuntimeEntityFactory.TryCreate(msg.Id, pos, _log, "EntitySpawn");
				if (created == null) // Unity object — ==
				{
					// ACCEPT-FIRST: the host could not materialize its own copy
					// (its mod set lacks the prefab), but a member that HAS the
					// prefab must still receive the creation, and the reporter's
					// report must still be acknowledged. The channel records the
					// acceptance and relays the ORIGINAL message — there is no
					// local copy to enrich.
					_world.ReportEntitySpawnUnmaterialized(sender, msg);
					return;
				}

				created.transform.eulerAngles = new Vector3(0f, 0f, msg.Rotation);
				created.gameObject.AddComponent<SpawnReplayMarker>(); // its Start must not re-report (scope check cannot see it — Start runs later)
			}

			RuntimeEntityCreation.Stamp(created, key);

			var relay = msg;
			var openable = created.GetComponent<Openable>();
			if (_session.Role == SessionRole.Host && openable is not null && openable.isKeypad
				&& msg.KeypadCode.Length == 0) // Unity object — ==
			{
				// The created keypad's code: the host generates it now (its
				// Random stream decides — same authority as the generation-time
				// keypad snapshot) and carries it in the relay. The creation
				// token rides along unchanged: it is the record's identity, not
				// part of the payload.
				relay = new EntitySpawnedMsg
				{
					Id = msg.Id,
					Position = msg.Position,
					Rotation = msg.Rotation,
					LiquidType = msg.LiquidType,
					KeypadCode = WorldEventSync.EnsureKeypadCode(openable),
					HasEnemyTint = msg.HasEnemyTint,
					EnemyTintColor = msg.EnemyTintColor,
					EnemyLightIntensity = msg.EnemyLightIntensity,
					IsAnimal = msg.IsAnimal,
					CreatorSteamId = msg.CreatorSteamId,
					CreationSequence = msg.CreationSequence,
				};
			}

			ApplyCreationData(created, relay, pos);
			ApplyEnemyTint(created, relay, pos);

			if (_session.Role == SessionRole.Host && sender != _session.LocalSteamId)
			{
				// Relay the creation to every member — the source included (it
				// keeps its local copy; the repeat is a no-op creation via the
				// creation key, and the source's keypad code — empty until this
				// arrives — is exactly what the carried code fills). The relay
				// carries the generated keypad code when the created entity is
				// one; a guest-created geyser's type travels unmodified (the
				// creating side's value is the authority).
				_world.SendEntitySpawned(relay);
			}

			_log.LogInformation("[EntitySpawn] created {Id} at {Pos} (creation {Creator}:{Sequence}).",
				msg.Id, pos, key.CreatorSteamId, key.CreationSequence);
		}
	}

	/// <summary>The creating side's next creation-instance token: this side's SteamId + a monotonic counter, so two creations are distinct even in the same cell and across peers.</summary>
	private RuntimeEntityKey NextCreationKey(string id, float x, float y)
	{
		_creationSequence++;
		return new RuntimeEntityKey(id, (int)Math.Floor(x), (int)Math.Floor(y), _session.LocalSteamId, _creationSequence);
	}

	/// <summary>Apply the creation-carried initial data: the keypad code NOW
	/// (the lazy generation skips an already-set code, Openable.cs:19 — no
	/// Start wait), the geyser's liquid type AFTER this copy's Start re-rolled
	/// it (the pump runs after Start, and the copy is located by its creation
	/// key — never by a 3 m first hit, which could write a sibling's type).</summary>
	private void ApplyCreationData(BuildingEntity created, EntitySpawnedMsg msg, Vector2 pos)
	{
		if (msg.KeypadCode.Length > 0)
		{
			var openable = created.GetComponent<Openable>();
			if (openable is not null && openable.isKeypad) // Unity object — ==
			{
				Traverse.Create(openable).Field("code").SetValue(msg.KeypadCode);
				_log.LogInformation("[EntitySpawn] applied carried keypad code at ({X:F1},{Y:F1}).", pos.x, pos.y);
			}
		}

		if (msg.LiquidType != 0 && created.GetComponentInChildren<GeyserScript>() != null) // Unity object — ==
		{
			_applyQueue.Add((RuntimeEntityKey.From(msg), pos, msg.LiquidType, Time.frameCount));
		}
	}

	/// <summary>Apply the creation-carried crystalenemy tint: the exact
	/// host/trigger-side post-SetColor color + light intensity, written directly
	/// (never the native SetColor — its per-side-random jitter would diverge).
	/// The copy was just created, so its Awake-assigned sprite/light fields are
	/// present; a non-crystalenemy entity with a stray flag is a no-op.</summary>
	private void ApplyEnemyTint(BuildingEntity created, EntitySpawnedMsg msg, Vector2 pos)
	{
		if (!msg.HasEnemyTint)
		{
			return;
		}

		var crystal = created.GetComponentInChildren<CrystalEnemy>();
		if (crystal == null) // Unity object — == (a non-crystalenemy prefab despite the flag — nothing to paint)
		{
			return;
		}

		var tint = msg.EnemyTintColor.ToNetColorRgba();
		CrystalEnemyTintAccess.ApplyTint(crystal, new Color(tint.R, tint.G, tint.B, tint.A), msg.EnemyLightIntensity);
		_log.LogInformation("[EntitySpawn] applied carried crystal tint at ({X:F1},{Y:F1}).", pos.x, pos.y);
	}

	/// <summary>The geyser reports run here — a frame after the parent Start, so
	/// the child's Start (GeyserScript.cs:12) has rolled the type and the
	/// creation message can carry it. The record is located by the QUEUED
	/// creation key and the report carries the QUEUED creation position: the
	/// geyser child's transform may sit in another cell, and reporting it made
	/// the host's record key differ from the death key.</summary>
	private void FlushReports()
	{
		if (_reportQueue.Count == 0)
		{
			return;
		}

		foreach (var (key, pos, rotation, atFrame, isAnimal) in _reportQueue)
		{
			if (Time.frameCount - atFrame < 1)
			{
				continue;
			}

			var geyser = FindGeyserByCreationKey(key);
			if (geyser == null) // Unity object — == (destroyed — the 60 s geyser-state cycle covers a lost report)
			{
				continue;
			}

			ReportSpawn(key, pos, rotation,
				Traverse.Create(geyser).Field("liquidType").GetValue<byte>(), "", isAnimal: isAnimal); // byte — exact type (a GetValue<int> cast throws InvalidCastException)
		}

		_reportQueue.RemoveAll(q => Time.frameCount - q.AtFrame >= 1);
	}

	/// <summary>Apply a received creation's carried liquid type — after this
	/// side's own copy Start re-rolled it (the pump runs after Start). The copy
	/// is located by its creation key, so a second geyser inside the 3 m
	/// neighbourhood can never receive the value.</summary>
	private void FlushApplies()
	{
		if (_applyQueue.Count == 0)
		{
			return;
		}

		foreach (var (key, pos, type, atFrame) in _applyQueue)
		{
			if (Time.frameCount - atFrame < 1)
			{
				continue;
			}

			var geyser = FindGeyserByCreationKey(key);
			if (geyser == null) // Unity object — == (already gone — nothing to align)
			{
				continue;
			}

			Traverse.Create(geyser).Field("liquidType").SetValue(type); // byte — exact type (a SetValue(int) cast throws ArgumentException)
			_log.LogInformation("[EntitySpawn] applied carried liquid type {Type} at ({X:F1},{Y:F1}).", type, pos.x, pos.y);
		}

		_applyQueue.RemoveAll(q => Time.frameCount - q.AtFrame >= 1);
	}

	private void ReportSpawn(RuntimeEntityKey key, Vector2 pos, float rotation, byte liquidType, string keypadCode,
		bool hasEnemyTint = false, NetColorRgba enemyTint = default, float enemyLightIntensity = 0f, bool isAnimal = false)
	{
		_log.LogInformation("[EntitySpawn] reporting {Id} at ({X:F1},{Y:F1}) (creation {Creator}:{Sequence}){Liquid}{Code}{Tint}{Animal}.",
			key.Id, pos.x, pos.y, key.CreatorSteamId, key.CreationSequence,
			liquidType != 0 ? $" (liquid {liquidType})" : "",
			keypadCode.Length > 0 ? " (keypad code carried)" : "",
			hasEnemyTint ? " (crystal tint carried)" : "",
			isAnimal ? " (animal — recovery owned by the enemy domain)" : "");
		_world.SendEntitySpawned(new EntitySpawnedMsg
		{
			Id = key.Id,
			Position = new NetVector2Msg(pos.x, pos.y),
			Rotation = rotation,
			LiquidType = liquidType,
			KeypadCode = keypadCode,
			HasEnemyTint = hasEnemyTint,
			EnemyTintColor = enemyTint.ToNetColorRgbaMsg(),
			EnemyLightIntensity = enemyLightIntensity,
			IsAnimal = isAnimal,
			CreatorSteamId = key.CreatorSteamId,
			CreationSequence = key.CreationSequence,
		});
	}

	/// <summary>A local copy this creation record binds to: the entity carrying
	/// the SAME creation key (prefab id + creation cell + creation-instance
	/// token) anywhere — the record's identity, drift-proof — or a MARKERLESS
	/// same-prefab copy inside the 1 m radius (a trap-layout materialization, an
	/// enemy-domain backfill copy or a generated entity: these never entered the
	/// runtime-creation tables, and their own <c>Start</c> can report them as
	/// creations). The judgment is the pure <see cref="RuntimeEntityMatch"/>;
	/// this scan only supplies the candidates. A candidate that CARRIES a marker
	/// is never a positional bind target — that is what swallowed a second
	/// same-cell creation.</summary>
	private static BuildingEntity? FindExisting(RuntimeEntityKey key, float x, float y)
	{
		var entities = Object.FindObjectsOfType<BuildingEntity>();
		var candidates = new List<RuntimeEntityMatch.Candidate>(entities.Length);
		foreach (var entity in entities)
		{
			var position = entity.transform.position;
			candidates.Add(new RuntimeEntityMatch.Candidate(
				entity.id,
				position.x,
				position.y,
				entity.GetComponent<TutorialClawProp>() != null, // Unity object — ==
				RuntimeEntityCreation.TryRead(entity, out var marker) ? marker : null));
		}

		var index = RuntimeEntityMatch.FindIndex(candidates, key, x, y);
		return index < 0 ? null : entities[index];
	}

	/// <summary>The copy carrying this EXACT creation key — no positional fallback: the deferred geyser report/apply must never bind a sibling.</summary>
	private static BuildingEntity? FindByCreationKey(RuntimeEntityKey key)
	{
		foreach (var entity in Object.FindObjectsOfType<BuildingEntity>())
		{
			if (RuntimeEntityCreation.TryRead(entity, out var marker) && marker == key)
			{
				return entity;
			}
		}

		return null;
	}

	/// <summary>The geyser child of the copy carrying this creation key — the deferred report/apply must never re-locate by proximity.</summary>
	private static GeyserScript? FindGeyserByCreationKey(RuntimeEntityKey key)
	{
		var entity = FindByCreationKey(key);
		return entity == null ? null : entity.GetComponentInChildren<GeyserScript>(); // Unity object — ==
	}
}
