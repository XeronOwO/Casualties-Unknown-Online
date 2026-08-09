using System;
using System.Linq;
using HarmonyLib;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// Layer-modifier boundary. The modifier decision (ApplyLayerModifiers,
/// WorldGeneration.cs:3729) reads the random stream AFTER the darken-wait
/// suspension, which the generation isolation does not restore (the
/// suspension's real-stream draws leak into the state it continues from) — so
/// a per-side roll picks a per-side modifier (host "虫害", guest "积水").
/// The host/solo side runs the game's own roll — its modifier is the world
/// definition; the guest side SKIPS the local roll entirely and applies the
/// host's modifier when it arrives with the generation snapshot
/// (LayerModifierSync). The guest stream's randomness past this point was
/// already per-side anyway, so skipping these 1-2 draws changes nothing that
/// was aligned.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "ApplyLayerModifiers")]
internal static class LayerModifierApplyPatch
{
	internal static Action<string>? Log;

	/// <summary>True when this side decides the modifier itself (host/solo) — guests apply the host's instead.</summary>
	internal static Func<bool>? IsModifierAuthority;

	/// <summary>The random stream state at this call's entry — captured on the
	/// host/solo side and broadcast with the generation snapshot so the guests
	/// replay the decision draws before Initialize (identical world effects).
	/// The guest side never captures (it never rolls).</summary>
	internal static byte[]? LastEntryState;

	private static bool Prefix(WorldGeneration __instance)
	{
		if (IsModifierAuthority?.Invoke() != true)
		{
			return false; // guest — the host's modifier arrives with the generation snapshot
		}

		LastEntryState = RandomStateSerializer.Serialize(Random.state);
		Log?.Invoke($"[LayerMod] enter state={BitConverter.ToString(LastEntryState).Replace("-", "")} chance={WorldGeneration.GetRunSettingFloat("layermodifierchance")} depth={__instance.biomeDepth} override={__instance.biomeOverride}");
		return true;
	}

	private static void Postfix(WorldGeneration __instance)
	{
		var active = LayerModifier.availableModifiers.FirstOrDefault(m => m.active);
		var prefix = (string?)AccessTools.Field(typeof(WorldGeneration), "layerPrefix")?.GetValue(__instance);
		Log?.Invoke($"[LayerMod] picked={active?.modifierIndex.ToString() ?? "none"} prefix={prefix}");
	}
}
