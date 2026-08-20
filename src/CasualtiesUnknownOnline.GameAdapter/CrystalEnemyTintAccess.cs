using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Reflection access to the CrystalEnemy presentation tint (CrystalEnemy.cs):
/// the private <c>sprite</c> / <c>light</c> fields assigned in Awake
/// (CrystalEnemy.cs:33-36) and the color/intensity the mimic triggers write via
/// <c>SetColor</c> (CrystalEnemy.cs:208-216). The tint is captured on the host
/// at spawn and carried as creation data, so the copy MUST be written the
/// EXACT captured color — never the native SetColor (it re-rolls the 0.9-1
/// per-channel jitter from the per-side random stream and would diverge).
/// <c>sprite</c>/<c>light</c> are read UNTYPED (the renderer types live outside
/// the game-reference graph, same as CrystalBehaviour.light); color/intensity
/// are engine properties accessed on the component instances. The
/// GameFieldContractTests rows lock both fields against a game update.
/// </summary>
internal static class CrystalEnemyTintAccess
{
	private const string SpriteFieldName = "sprite";
	private const string LightFieldName = "light";

	/// <summary>The host's authoritative current tint — the EXACT post-SetColor
	/// values (jitter included). False when a field is missing (a game update —
	/// the caller then carries no tint, keeping the old colorless behaviour).</summary>
	internal static bool TryRead(CrystalEnemy enemy, out Color color, out float lightIntensity)
	{
		color = default;
		lightIntensity = 0f;
		var sprite = Traverse.Create(enemy).Field(SpriteFieldName).GetValue();
		var light = Traverse.Create(enemy).Field(LightFieldName).GetValue();
		if (sprite == null || light == null) // engine objects — == (null when Awake did not run or the field is gone)
		{
			return false;
		}

		var spriteColor = Traverse.Create(sprite).Property("color");
		var intensity = Traverse.Create(light).Property("intensity");
		if (!spriteColor.PropertyExists() || !intensity.PropertyExists())
		{
			return false;
		}

		color = spriteColor.GetValue<Color>();
		lightIntensity = intensity.GetValue<float>();
		return true;
	}

	/// <summary>Write the EXACT host color + intensity onto the copy (never the
	/// native SetColor — its jitter would diverge). The sprite and the light
	/// share the color; the light carries its own intensity (CrystalEnemy.cs:213-215).</summary>
	internal static void ApplyTint(CrystalEnemy enemy, Color color, float lightIntensity)
	{
		var sprite = Traverse.Create(enemy).Field(SpriteFieldName).GetValue();
		var light = Traverse.Create(enemy).Field(LightFieldName).GetValue();
		if (sprite != null)
		{
			Traverse.Create(sprite).Property("color").SetValue(color);
		}

		if (light != null)
		{
			Traverse.Create(light).Property("color").SetValue(color);
			Traverse.Create(light).Property("intensity").SetValue(lightIntensity);
		}
	}
}
