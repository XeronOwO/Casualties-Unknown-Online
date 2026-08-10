using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Runtime world-entity creation sync (the spawn command): a BuildingEntity
/// starting OUTSIDE world generation is a runtime creation — the creating side
/// keeps its local copy and reports (id + position + rotation); the host
/// creates its own copy and relays; every receiving side creates the same
/// entity at the same place, which is what makes the position-keyed identity
/// (the entity-event channel) hold for runtime creations too. Items do NOT
/// ride this channel — the item domain already syncs runtime item creation.
/// World-generation entities are skipped: they are deterministic on both sides.
/// </summary>
internal sealed class EntitySpawnSync(IWorldControl world, ISessionControl session, ILogger<EntitySpawnSync> log)
{
	private readonly IWorldControl _world = world;
	private readonly ISessionControl _session = session;
	private readonly ILogger<EntitySpawnSync> _log = log;

	internal void BindToSession() => _world.EntitySpawnedReceived += OnRemoteEntitySpawned;

	internal void Unbind() => _world.EntitySpawnedReceived -= OnRemoteEntitySpawned;

	/// <summary>
	/// Patch-bridge entry: a world entity just started. Inside world generation
	/// = deterministic (both sides generate the same entity — nothing to do); a
	/// RemoteApply create = a replay of this very channel (nothing to do);
	/// anything else in a session = a runtime creation — report it.
	/// </summary>
	internal void OnEntityInstantiated(BuildingEntity entity)
	{
		if (CallContext.Current == CallContext.Origin.RemoteApply || !_session.SessionActive || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		if (string.IsNullOrEmpty(entity.id))
		{
			return; // no prefab id — nothing to recreate on the peers
		}

		var pos = entity.transform.position;
		_log.LogInformation("[EntitySpawn] reporting {Id} at ({X:F1},{Y:F1}).", entity.id, pos.x, pos.y);
		_world.SendEntitySpawned(new EntitySpawnedMsg
		{
			Id = entity.id,
			Position = new NetVector2Msg(pos.x, pos.y),
			Rotation = entity.transform.eulerAngles.z,
		});
	}

	private void OnRemoteEntitySpawned(ulong sender, EntitySpawnedMsg msg)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var pos = new Vector2(msg.Position.X, msg.Position.Y);
			if (AlreadyExists(msg.Id, pos))
			{
				return; // idempotent — a repeated report is a no-op
			}

			var created = Utils.Create(msg.Id, pos, 0f);
			if (created == null) // Unity object — == (unknown id — the sender's mod/prefab set differs)
			{
				_log.LogWarning("[EntitySpawn] cannot create {Id} at {Pos}.", msg.Id, pos);
				return;
			}

			created.transform.eulerAngles = new Vector3(0f, 0f, msg.Rotation);
			_log.LogInformation("[EntitySpawn] created {Id} at {Pos}.", msg.Id, pos);
		}
	}

	/// <summary>A same-id entity within the matching radius already exists (a
	/// repeated report, or a naturally-matching entity covers the position).</summary>
	private static bool AlreadyExists(string id, Vector2 pos)
	{
		foreach (var entity in Object.FindObjectsOfType<BuildingEntity>())
		{
			if (entity.id == id && Vector2.Distance(entity.transform.position, pos) < 3f)
			{
				return true;
			}
		}

		return false;
	}
}
