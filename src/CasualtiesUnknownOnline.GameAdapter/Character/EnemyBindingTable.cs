using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The enemy identity tables BOTH roles share: which local animal carries which
/// host id, and back. The host binds an id at its first capture (and records the
/// match hint separately); the guest binds when a roster row is adopted. Every
/// removal goes through one operation over both directions, so a half-removed id
/// can never survive — the defect the old pair of dictionaries could produce
/// when one side was pruned and the other was not.
/// </summary>
internal sealed class EnemyBindingTable
{
	private readonly Dictionary<BuildingEntity, NetworkEntityId> _idByEntity = [];
	private readonly Dictionary<NetworkEntityId, BuildingEntity> _entityById = [];

	internal int Count => _entityById.Count;

	/// <summary>Every live binding: the host's capture and the guest's stale-row removal both read this instead of a one-frame scene scan.</summary>
	internal IEnumerable<(NetworkEntityId Id, BuildingEntity Entity)> Bound
	{
		get
		{
			foreach (var pair in _entityById)
			{
				yield return (pair.Key, pair.Value);
			}
		}
	}

	internal IEnumerable<NetworkEntityId> Ids => _entityById.Keys;

	internal bool TryGetId(BuildingEntity entity, out NetworkEntityId id) => _idByEntity.TryGetValue(entity, out id);

	internal bool IsBound(BuildingEntity entity) => _idByEntity.ContainsKey(entity);

	internal bool Contains(NetworkEntityId id) => _entityById.ContainsKey(id);

	internal BuildingEntity? Find(NetworkEntityId id) => _entityById.TryGetValue(id, out var entity) ? entity : null;

	internal void Bind(BuildingEntity entity, NetworkEntityId id)
	{
		_idByEntity[entity] = id;
		_entityById[id] = entity;
	}

	/// <summary>Remove one id from both directions; returns the entity it named (Unity-null when the object is already gone). The reverse key is removed by REFERENCE — a destroyed Unity object is still the dictionary's own key.</summary>
	internal BuildingEntity? Unbind(NetworkEntityId id)
	{
		if (!_entityById.TryGetValue(id, out var entity))
		{
			return null;
		}

		_entityById.Remove(id);
		_idByEntity.Remove(entity);
		return entity;
	}

	/// <summary>Every id whose Unity object no longer exists — the confirmed death. The bindings are removed here; the caller drops the hint, the sets and the presentation record.</summary>
	internal List<(NetworkEntityId Id, BuildingEntity Entity)> PruneDead()
	{
		List<NetworkEntityId>? dead = null;
		foreach (var pair in _entityById)
		{
			if (pair.Value == null) // Unity object — ==
			{
				(dead ??= []).Add(pair.Key);
			}
		}

		var removed = new List<(NetworkEntityId, BuildingEntity)>(dead?.Count ?? 0);
		if (dead is null)
		{
			return removed;
		}

		foreach (var id in dead)
		{
			var entity = _entityById[id];
			_entityById.Remove(id);
			_idByEntity.Remove(entity);
			removed.Add((id, entity));
		}

		return removed;
	}

	internal void Clear()
	{
		_idByEntity.Clear();
		_entityById.Clear();
	}
}
