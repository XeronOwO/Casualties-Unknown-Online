using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Runtime world-entity creation sync (the spawn command): a BuildingEntity
/// starting OUTSIDE world generation is a runtime creation — the creating side
/// keeps its local copy and reports (id + position + rotation + creation-time
/// initial data); the host creates its own copy, generates the keypad code if
/// the created entity is a keypad (its Random stream is the authority) and
/// relays to every member (the source included — the relay is idempotent via
/// AlreadyExists, and the source's keypad code may need the carried value);
/// every receiving side creates the same entity at the same place and applies
/// the carried data. World-generation entities are skipped: they are
/// deterministic on both sides. Items do NOT ride this channel.
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

	/// <summary>Geyser creations awaiting their child Start (value tuples — no Unity references held).</summary>
	private readonly List<(string Id, Vector2 Pos, float Rotation, int AtFrame)> _reportQueue = [];

	/// <summary>Received geyser creations awaiting their own copy's Start before the carried type is applied.</summary>
	private readonly List<(Vector2 Pos, byte Type, int AtFrame)> _applyQueue = [];

	internal void BindToSession() => _world.EntitySpawnedReceived += OnRemoteEntitySpawned;

	internal void Unbind() => _world.EntitySpawnedReceived -= OnRemoteEntitySpawned;

	internal void Update()
	{
		FlushReports();
		FlushApplies();
	}

	/// <summary>
	/// Patch-bridge entry: a world entity just started. Inside world generation
	/// = deterministic (both sides generate the same entity — nothing to do); a
	/// RemoteApply create = a replay of this very channel (nothing to do);
	/// anything else in a session = a runtime creation — report it. A geyser's
	/// report is deferred ONE frame: its liquid type rolls at the CHILD's Start
	/// (GeyserScript.cs:12), which runs after this parent Start — the creation
	/// message carries the initial data once it exists. A created keypad's code
	/// is generated HOST-side now (the host's Random stream is the authority).
	/// </summary>
	internal void OnEntityInstantiated(BuildingEntity entity)
	{
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
		if (entity.GetComponentInChildren<GeyserScript>() != null) // Unity object — ==
		{
			_reportQueue.Add((entity.id, pos, entity.transform.eulerAngles.z, Time.frameCount));
			return;
		}

		var openable = entity.GetComponent<Openable>();
		var keypadCode = openable is not null && openable.isKeypad && _session.Role == SessionRole.Host
			? WorldEventSync.EnsureKeypadCode(openable) // the host creates it — its code is host authority from the start
			: "";
		ReportSpawn(entity.id, pos, entity.transform.eulerAngles.z, 0, keypadCode);
	}

	private void OnRemoteEntitySpawned(ulong sender, EntitySpawnedMsg msg)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var pos = new Vector2(msg.Position.X, msg.Position.Y);
			var created = FindExisting(msg.Id, pos);
			if (created == null)
			{
				var createdGo = Utils.Create(msg.Id, pos, 0f);
				if (createdGo == null) // Unity object — == (unknown id — the sender's mod/prefab set differs)
				{
					_log.LogWarning("[EntitySpawn] cannot create {Id} at {Pos}.", msg.Id, pos);
					return;
				}

				created = createdGo.GetComponent<BuildingEntity>();
				if (created == null) // Unity object — ==
				{
					_log.LogWarning("[EntitySpawn] created {Id} at {Pos} has no BuildingEntity.", msg.Id, pos);
					return;
				}

				created.transform.eulerAngles = new Vector3(0f, 0f, msg.Rotation);
				created.gameObject.AddComponent<SpawnReplayMarker>(); // its Start must not re-report (scope check cannot see it — Start runs later)
			}

			var relay = msg;
			var openable = created.GetComponent<Openable>();
			if (_session.Role == SessionRole.Host && openable is not null && openable.isKeypad
				&& msg.KeypadCode.Length == 0) // Unity object — ==
			{
				// The created keypad's code: the host generates it now (its
				// Random stream decides — same authority as the generation-time
				// keypad snapshot) and carries it in the relay.
				relay = new EntitySpawnedMsg
				{
					Id = msg.Id,
					Position = msg.Position,
					Rotation = msg.Rotation,
					LiquidType = msg.LiquidType,
					KeypadCode = WorldEventSync.EnsureKeypadCode(openable),
				};
			}

			ApplyCreationData(created, relay, pos);

			if (_session.Role == SessionRole.Host && sender != _session.LocalSteamId)
			{
				// Relay the creation to every member — the source included (it
				// keeps its local copy; the repeat is a no-op creation via
				// FindExisting, and the source's keypad code — empty until this
				// arrives — is exactly what the carried code fills). The relay
				// carries the generated keypad code when the created entity is
				// one; a guest-created geyser's type travels unmodified (the
				// creating side's value is the authority).
				_world.SendEntitySpawned(relay);
			}

			_log.LogInformation("[EntitySpawn] created {Id} at {Pos}.", msg.Id, pos);
		}
	}

	/// <summary>Apply the creation-carried initial data: the keypad code NOW
	/// (the lazy generation skips an already-set code, Openable.cs:19 — no
	/// Start wait), the geyser's liquid type AFTER this copy's Start re-rolled
	/// it (the pump runs after Start).</summary>
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
			_applyQueue.Add((pos, msg.LiquidType, Time.frameCount));
		}
	}

	/// <summary>The geyser reports run here — a frame after the parent Start, so
	/// the child's Start (GeyserScript.cs:12) has rolled the type and the
	/// creation message can carry it.</summary>
	private void FlushReports()
	{
		if (_reportQueue.Count == 0)
		{
			return;
		}

		foreach (var (id, pos, rotation, atFrame) in _reportQueue)
		{
			if (Time.frameCount - atFrame < 1)
			{
				continue;
			}

			var geyser = TrapEffectApplier.FindTrap<GeyserScript>(pos);
			if (geyser == null) // Unity object — == (destroyed — the 60 s geyser-state cycle covers a lost report)
			{
				continue;
			}

			var p = geyser.transform.position;
			ReportSpawn(id, new Vector2(p.x, p.y), rotation,
				Traverse.Create(geyser).Field("liquidType").GetValue<byte>(), ""); // byte — exact type (a GetValue<int> cast throws InvalidCastException)
		}

		_reportQueue.RemoveAll(q => Time.frameCount - q.AtFrame >= 1);
	}

	/// <summary>Apply a received creation's carried liquid type — after this
	/// side's own copy Start re-rolled it (the pump runs after Start).</summary>
	private void FlushApplies()
	{
		if (_applyQueue.Count == 0)
		{
			return;
		}

		foreach (var (pos, type, atFrame) in _applyQueue)
		{
			if (Time.frameCount - atFrame < 1)
			{
				continue;
			}

			var geyser = TrapEffectApplier.FindTrap<GeyserScript>(pos);
			if (geyser == null) // Unity object — == (already gone — nothing to align)
			{
				continue;
			}

			Traverse.Create(geyser).Field("liquidType").SetValue(type); // byte — exact type (a SetValue(int) cast throws ArgumentException)
			_log.LogInformation("[EntitySpawn] applied carried liquid type {Type} at ({X:F1},{Y:F1}).", type, pos.x, pos.y);
		}

		_applyQueue.RemoveAll(q => Time.frameCount - q.AtFrame >= 1);
	}

	private void ReportSpawn(string id, Vector2 pos, float rotation, byte liquidType, string keypadCode)
	{
		_log.LogInformation("[EntitySpawn] reporting {Id} at ({X:F1},{Y:F1}){Liquid}{Code}.",
			id, pos.x, pos.y,
			liquidType != 0 ? $" (liquid {liquidType})" : "",
			keypadCode.Length > 0 ? " (keypad code carried)" : "");
		_world.SendEntitySpawned(new EntitySpawnedMsg
		{
			Id = id,
			Position = new NetVector2Msg(pos.x, pos.y),
			Rotation = rotation,
			LiquidType = liquidType,
			KeypadCode = keypadCode,
		});
	}

	/// <summary>A same-id entity within the matching radius already exists (a
	/// repeated report — the same position's float noise — or a naturally-
	/// matching entity covers the position). The radius is 1, not 3: a 3 m
	/// radius absorbed consecutive spawns of the same entity (the observed
	/// bug — three spawned turrets ~1-2 m apart, only the first reached the
	/// peer).</summary>
	private static BuildingEntity? FindExisting(string id, Vector2 pos)
	{
		foreach (var entity in Object.FindObjectsOfType<BuildingEntity>())
		{
			if (entity.id == id && Vector2.Distance(entity.transform.position, pos) < 1f)
			{
				return entity;
			}
		}

		return null;
	}

}
