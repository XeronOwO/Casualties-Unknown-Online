using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The crafting list draws every recipe through <see cref="Recipe.resultSprite"/>, which loads the
/// product's prefab by id and takes its sprite (<c>Resources.Load&lt;GameObject&gt;(id)</c>,
/// <c>reversing/release-1.0/Assembly-CSharp/Recipe.cs:10-20</c>). A mod item has no game prefab, so
/// the load returns null and the getter throws inside <c>PlayerCamera.RefreshRecipeList</c> — the
/// crafting screen then never opens, for every player, not only the one who owns the item
/// (measured 2026-11-14 with the example mod's <c>example.dressing</c>; the game's own recipes all
/// have prefabs and never take this branch).
/// <para>
/// The framework owns content registration, so it owns this guarantee: a recipe whose product has
/// no prefab still renders — with a placeholder — and says which id it stood in for, once. The
/// alternative, letting the throw reach the game's list, takes a whole screen away from everyone
/// because one mod declared an item.
/// </para>
/// </summary>
internal static class RecipeSpriteFallbackPatch
{
	private static readonly HashSet<string> Warned = [];

	private static Sprite? _placeholder;

	private static Sprite Placeholder()
	{
		if (_placeholder != null)
		{
			return _placeholder;
		}

		var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
		texture.SetPixel(0, 0, new Color(0.55f, 0.55f, 0.55f, 1f));
		texture.Apply();
		_placeholder = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
		return _placeholder;
	}

	[HarmonyPatch(typeof(Recipe), "get_resultSprite")]
	internal static class ResultSpritePatch
	{
		private static bool Prefix(Recipe __instance, ref ValueTuple<Sprite, Color> __result)
		{
			var result = __instance.result;
			if (result.isLiquid)
			{
				return true;
			}

			if (Resources.Load<GameObject>(result.id) != null)
			{
				return true;
			}

			__result = new ValueTuple<Sprite, Color>(Placeholder(), Color.white);
			if (Warned.Add(result.id))
			{
				Debug.LogWarning(
					$"[Recipe] {result.id} has no prefab to draw; the crafting list shows a placeholder for it. "
					+ "The recipe still works; only its icon is missing.");
			}

			return false;
		}
	}
}
