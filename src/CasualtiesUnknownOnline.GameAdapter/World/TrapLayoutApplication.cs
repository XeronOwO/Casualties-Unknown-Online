using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Guest side: the host's trap layout arrived — align the local world: the
/// missing entries materialize from the host's prefab names (a structural
/// member's name fails Resources.Load and is skipped with a trace — it
/// depends on the structure's deterministic placement), the surplus/off-
/// position entities destroy. The alignment judgment is pure
/// (TrapLayoutAlign — the same 3-unit radius the position-key replay uses).
/// A snapshot arriving during generation defers to the pump. RemoteApply: the
/// materialization/destroy must never re-report.
/// </summary>
internal sealed class TrapLayoutApplication(IWorldControl world, ILogger<TrapLayoutApplication> log)
{
	private readonly IWorldControl _world = world;
	private readonly ILogger<TrapLayoutApplication> _log = log;

	private List<TrapLayoutEntryMsg>? _pending;
	private bool _generating;

	internal void BindToSession() => _world.TrapLayoutReceived += OnTrapLayoutReceived;

	internal void Unbind() => _world.TrapLayoutReceived -= OnTrapLayoutReceived;

	/// <summary>Pump: apply a deferred snapshot once generation finished.</summary>
	internal void Update()
	{
		var generating = HarmonyTraverse.IsGenerating();
		if (_pending is { } pending && !generating && _generating)
		{
			_pending = null;
			Apply(pending);
		}

		_generating = generating;
	}

	private void OnTrapLayoutReceived(IReadOnlyList<TrapLayoutEntryMsg> entries)
	{
		if (HarmonyTraverse.IsGenerating())
		{
			_pending = [.. entries]; // applied by the pump once generation ends
			return;
		}

		Apply(entries);
	}

	private void Apply(IReadOnlyList<TrapLayoutEntryMsg> hostLayout)
	{
		var local = TrapEntityScan.Scan();
		var localEntries = new List<TrapLayoutEntryMsg>(local.Count);
		foreach (var entity in local)
		{
			localEntries.Add(entity.Entry);
		}

		var alignment = TrapLayoutAlign.Align(hostLayout, localEntries);
		_log.LogInformation("[TrapLayout] aligning: {Spawn} to materialize, {Destroy} to destroy.", alignment.ToSpawn.Count, alignment.ToDestroy.Count);

		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			foreach (var index in alignment.ToDestroy)
			{
				var entity = local[index].Component;
				_log.LogInformation("[TrapLayout] destroying surplus {Kind} at ({X:F1},{Y:F1}).",
					localEntries[index].Kind, entity.transform.position.x, entity.transform.position.y);
				Object.Destroy(entity.gameObject);
			}

			foreach (var entry in alignment.ToSpawn)
			{
				Materialize(entry);
			}
		}
	}

	private void Materialize(TrapLayoutEntryMsg entry)
	{
		var prefab = Resources.Load(entry.PrefabName);
		if (prefab == null) // Unity object — == (a structural member's name is not a loadable prefab)
		{
			_log.LogInformation("[TrapLayout] {Kind} at ({X:F1},{Y:F1}) has no loadable prefab '{Prefab}' — skipped (structural member, the structure's placement is deterministic).",
				entry.Kind, entry.X, entry.Y, entry.PrefabName);
			return;
		}

		var go = Object.Instantiate((GameObject)prefab, new Vector3(entry.X, entry.Y, 0f), Quaternion.identity);
		_log.LogInformation("[TrapLayout] materialized {Kind} at ({X:F1},{Y:F1}) from '{Prefab}'.",
			entry.Kind, entry.X, entry.Y, entry.PrefabName);
	}
}
