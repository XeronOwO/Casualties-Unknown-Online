using CasualtiesUnknownOnline.GameAdapter.Content;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The single item-prefab resolution seam used by CUO's own materialization
/// paths. Vanilla item prefabs keep the normal <c>Resources.Load</c> behavior;
/// mod-registered custom item templates are served from the content provider.
/// This keeps every restore/spawn path (including the save restore that calls
/// <c>Resources.Load</c> directly in vanilla code) able to materialize a
/// custom item without exposing game types to mods.
/// </summary>
internal static class ItemPrefabResolver
{
	internal static bool TryGetCustomTemplate(string id, out GameObject? template)
	{
		if (!string.IsNullOrWhiteSpace(id)
			&& PatchBridge.Impl?.TryResolveItemTemplate(id, out template) == true
			&& template != null) // Unity object — ==
		{
			return true;
		}

		template = null;
		return false;
	}

	internal static GameObject? Load(string id)
	{
		if (TryGetCustomTemplate(id, out var custom))
		{
			return custom;
		}

		return Resources.Load<GameObject>(id);
	}

	/// <summary>
	/// The IL-transpiler resource fallback: mirrors <c>Resources.Load(string)</c>
	/// but serves a custom template when the id has one.
	/// </summary>
	internal static Object? LoadResource(string id)
	{
		if (TryGetCustomTemplate(id, out var custom))
		{
			return custom;
		}

		return Resources.Load(id);
	}

	/// <summary>
	/// The IL-transpiler instantiate fallback: mirrors
	/// <c>Object.Instantiate(Object, Vector3, Quaternion)</c> and activates the
	/// clone when the source is a CUO custom item template (the cached template
	/// is inactive by design).
	/// </summary>
	internal static Object? InstantiateResource(
		Object original,
		Vector3 position,
		Quaternion rotation)
	{
		var instance = Object.Instantiate(original!, position, rotation);
		if (instance is GameObject gameObject
			&& original is GameObject source
			&& source.GetComponent<CustomItemTemplateMarker>() != null) // Unity object — ==
		{
			gameObject.SetActive(true);
		}

		return instance;
	}
}
