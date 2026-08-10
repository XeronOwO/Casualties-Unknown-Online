using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.WorldGen;

/// <summary>
/// Layer-modifier boundary. The modifier decision (ApplyLayerModifiers,
/// WorldGeneration.cs:3729) reads the random stream AFTER the darken-wait
/// suspension — the segment restore happens at the end of that suspension, and
/// the world's frame-level draws between the restore and this call leak into
/// the public stream per-side (frame-rate dependent; observed 123-151 ms
/// windows), so a per-side roll picks a per-side modifier (host "虫害", guest
/// "积水").
/// BOTH sides rewind the decision to the last segment start
/// (WorldGenRandomIsolation.LastSegmentStart — fingerprint-identical on every
/// side, e.g. 1A8B716A… on both) before drawing: the roll becomes a pure
/// function of the segment state, identical on every side, and the world
/// effects (Flooded's liquid fills, Infested/Ionized's entity distributions)
/// land in identical positions.
/// The host/solo side then runs the game's own roll unchanged (its modifier is
/// the world definition); the guest side replays the same draws locally —
/// filling layerPrefix BEFORE the entry banner is built (WorldGeneration.cs:3648,
/// so the guest's banner shows the modifier like the host's) — and defers
/// Initialize until generation finishes (LayerModifierSync; mid-generation
/// Initialize conflicts with the terrain writes). The snapshot-carried index
/// + random state remain the authoritative fallback for world entries outside
/// a generation (solo→lobby, mid-session join).
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "ApplyLayerModifiers")]
internal static class LayerModifierApplyPatch
{
	internal static Action<string>? Log;

	/// <summary>True when this side decides the modifier itself (host/solo) — guests replay the decision locally and fall back to the snapshot.</summary>
	internal static Func<bool>? IsModifierAuthority;

	/// <summary>The random stream state at this call's entry (rewound to the
	/// last segment start) — captured on the host/solo side and broadcast with
	/// the generation snapshot so a world entry outside a generation can replay
	/// the decision draws before Initialize.</summary>
	internal static byte[]? LastEntryState;

	/// <summary>The guest's local replay result (modifier index, -1 = none) plus
	/// the stream state at the decision entry (rewound segment start) and right
	/// after its draws — the adapter defers Initialize until generation
	/// finishes and checks the entry state against the host's snapshot one
	/// (baseline-divergence detection).</summary>
	internal static Action<int, byte[]?, byte[]?>? ReportLocalDecision;

	private static bool Prefix(WorldGeneration __instance)
	{
		var start = WorldGenRandomIsolation.LastSegmentStart;
		if (start is not null)
		{
			// Rewind the decision to the segment start — the draws must begin
			// from the state that is identical on every side, not from wherever
			// the post-restore frame-level leaks left the public stream.
			Random.state = start.Value;
		}

		if (IsModifierAuthority?.Invoke() != true)
		{
			// Guest: replay the decision locally — same draws, same pool as the
			// game's own roll (WorldGeneration.cs:3730-3745; PickRandom =
			// Random.Range(0, pool.Count), Utils.cs:45), so the roll and the
			// stream position match the host's. The prefix/description write
			// lands BEFORE the entry banner is built (WorldGeneration.cs:3648),
			// so the guest's banner shows the modifier like the host's does.
			var entryState = RandomStateSerializer.Serialize(Random.state);
			var index = -1;
			if (Random.value < WorldGeneration.GetRunSettingFloat("layermodifierchance") * 0.01f)
			{
				List<LayerModifier> pool = __instance.biomeDepth <= 1
					? [.. LayerModifier.availableModifiers.Where(x => !x.hideOnFirstLayer)]
					: [.. LayerModifier.availableModifiers];
				index = pool[Random.Range(0, pool.Count)].modifierIndex;
				AccessTools.Field(typeof(WorldGeneration), "layerPrefix")?.SetValue(__instance, Locale.GetOther("layermodifier" + index));
				AccessTools.Field(typeof(WorldGeneration), "layerDescription")?.SetValue(__instance, Locale.GetOther("layermodifier" + index + "dsc"));
			}

			var afterState = RandomStateSerializer.Serialize(Random.state);
			ReportLocalDecision?.Invoke(index, entryState, afterState);
			Log?.Invoke($"[LayerMod] guest replay index={index} depth={__instance.biomeDepth} entryState={BitConverter.ToString(entryState).Replace("-", "")} afterState={BitConverter.ToString(afterState).Replace("-", "")}");
			return false; // the game's roll never runs on the guest side
		}

		LastEntryState = RandomStateSerializer.Serialize(Random.state);
		Log?.Invoke($"[LayerMod] enter state={BitConverter.ToString(LastEntryState).Replace("-", "")} chance={WorldGeneration.GetRunSettingFloat("layermodifierchance")} depth={__instance.biomeDepth} override={__instance.biomeOverride}");
		return true;
	}

	private static void Postfix(WorldGeneration __instance)
	{
		var active = LayerModifier.availableModifiers.FirstOrDefault(m => m.active);
		var prefix = (string?)AccessTools.Field(typeof(WorldGeneration), "layerPrefix")?.GetValue(__instance);
		Log?.Invoke($"[LayerMod] picked={active?.modifierIndex.ToString() ?? "none"} prefix={prefix} afterState={BitConverter.ToString(RandomStateSerializer.Serialize(Random.state)).Replace("-", "")}");
	}
}
