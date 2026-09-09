using System;
using CasualtiesUnknownOnline.GameAdapter.Items;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Materializes CUO custom item and building templates from <c>Utils.Create</c>
/// when no vanilla prefab exists for the requested id. The native method stays
/// on the vanilla path (including its throw-on-missing behavior); the prefix
/// only takes over for ids that have a mod-registered runtime template.
/// <para>
/// A mod's instance hook that throws must not leave a half-built instance
/// behind: the instance is destroyed and the exception rethrown, so the
/// caller's per-entry containment (RuntimeEntityFactory) sees a clean failure
/// instead of an unmarked orphan that exists on one side only.
/// </para>
/// </summary>
internal static class UtilsCreateCustomPrefabPatch
{
	[HarmonyPatch(typeof(Utils), "Create", [typeof(string), typeof(Vector2), typeof(float)])]
	internal static class PositionPatch
	{
		private static bool Prefix(string id, Vector2 pos, float rot, ref GameObject __result)
		{
			if (string.IsNullOrWhiteSpace(id) || !TryResolveCustomTemplate(id, out var template))
			{
				return true;
			}

			var created = Materialize(id, () => Object.Instantiate(
				template, new Vector3(pos.x, pos.y, 0f), Quaternion.Euler(0f, 0f, rot)) as GameObject);
			if (created == null) // Unity object — ==
			{
				__result = null!;
				return false;
			}

			__result = created;
			return false;
		}
	}

	[HarmonyPatch(typeof(Utils), "Create", [typeof(string), typeof(Transform)])]
	internal static class TransformPatch
	{
		private static bool Prefix(string id, Transform trans, ref GameObject __result)
		{
			if (string.IsNullOrWhiteSpace(id) || !TryResolveCustomTemplate(id, out var template))
			{
				return true;
			}

			var created = Materialize(id, () => Object.Instantiate(template, trans) as GameObject);
			if (created == null) // Unity object — ==
			{
				__result = null!;
				return false;
			}

			__result = created;
			return false;
		}
	}

	/// <summary>
	/// Instantiate the custom template and run the mod's instance hooks. A hook
	/// that THROWS destroys the still-inactive instance and rethrows — never a
	/// half-built, unmarked orphan in the world.
	/// </summary>
	private static GameObject? Materialize(string id, Func<GameObject?> instantiate)
	{
		var created = instantiate();
		if (created == null) // Unity object — ==
		{
			return null;
		}

		try
		{
			PatchBridge.Impl?.ApplyCustomBuildingInstanceHooks(id, created);
		}
		catch
		{
			Object.Destroy(created);
			throw;
		}

		created.SetActive(true); // the cached template is inactive; every instance must be live
		return created;
	}

	private static bool TryResolveCustomTemplate(string id, out GameObject? template)
	{
		if (ItemPrefabResolver.TryGetCustomTemplate(id, out var itemTemplate))
		{
			template = itemTemplate;
			return true;
		}

		if (PatchBridge.Impl?.TryResolveBuildingTemplate(id, out var buildingTemplate) == true
			&& buildingTemplate != null) // Unity object — ==
		{
			template = buildingTemplate;
			return true;
		}

		template = null;
		return false;
	}
}
