using System;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Object = UnityEngine.Object;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The one materialization path for a runtime-created prefab (the entity-spawn
/// channel and the enemy runtime-spawn backfill share it). Two rules that must
/// never drift apart:
/// <list type="bullet">
/// <item>NO <c>Resources.Load</c> pre-check. A MOD-registered template lives in
/// the content provider and is materialized by <c>UtilsCreateCustomPrefabPatch</c>
/// — it is not in Resources BY DESIGN, so a pre-check permanently starved every
/// mod building and every mod animal (round-3 finding). The attempt itself is
/// the check.</item>
/// <item><c>Utils.Create</c> THROWS on a missing prefab (observed 2026-08-15:
/// one snapshot packet killed the rest of the Steam receive batch), so the
/// throw is contained per entry and a non-BuildingEntity orphan is destroyed —
/// a local-only object would silently diverge from every peer.</item>
/// </list>
/// </summary>
internal static class RuntimeEntityFactory
{
	/// <summary>
	/// Materialize <paramref name="prefabId"/> at <paramref name="pos"/> and
	/// return its BuildingEntity, or null (logged) when it cannot be
	/// materialized. <paramref name="context"/> names the caller in the log.
	/// </summary>
	internal static BuildingEntity? TryCreate(string prefabId, Vector2 pos, ILogger log, string context)
	{
		GameObject createdGo;
		try
		{
			createdGo = Utils.Create(prefabId, pos, 0f);
		}
		catch (Exception ex)
		{
			log.LogWarning(ex, "[{Context}] cannot create {Id} at ({X:F1},{Y:F1}) — Utils.Create threw (missing prefab or template).",
				context, prefabId, pos.x, pos.y);
			return null;
		}

		if (createdGo == null) // Unity object — == (an Instantiate wrapper may return null)
		{
			log.LogWarning("[{Context}] cannot create {Id} at ({X:F1},{Y:F1}) — Utils.Create returned nothing.",
				context, prefabId, pos.x, pos.y);
			return null;
		}

		var created = createdGo.GetComponent<BuildingEntity>();
		if (created == null) // Unity object — ==
		{
			log.LogWarning("[{Context}] created {Id} at ({X:F1},{Y:F1}) has no BuildingEntity — destroyed the orphan instead of leaving a local-only object.",
				context, prefabId, pos.x, pos.y);
			Object.Destroy(createdGo);
			return null;
		}

		return created;
	}
}
