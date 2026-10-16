using HarmonyLib;
using CasualtiesUnknownOnline.GameAdapter.World;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The host rule that parks the radiation line above the layer floor. The native line has NO floor stop at
/// all: its <c>Update</c> adds to an uncapped <c>timeGone</c> and derives the position from it, so it passes
/// the floor and keeps descending. This postfix caps that one value and recomputes the position with the
/// game's own formula.
///
/// <para>
/// It runs on BOTH sides on purpose. A guest keeps running its local <c>RadiationLine.Update</c> between the
/// host's 5 Hz broadcasts, so a host-only clamp would be outrun locally; the rule value travels on the host
/// rule surface and the formula is pure, so the two sides converge without a new wire field. Nothing else
/// reads <c>timeGone</c> except the HUD's depth marker and the total-depth statistic, both of which read the
/// position this recomputes — so freezing the descent changes no damage rule and no layer progression.
/// </para>
/// </summary>
[HarmonyPatch(typeof(RadiationLine), "Update")]
internal static class RadiationLineFloorStopPatch
{
	[HarmonyPostfix]
	private static void Postfix(RadiationLine __instance)
	{
		if (PatchBridge.Impl?.RadiationLineCeiling is not { } ceiling)
		{
			return;
		}

		if (RadiationLineTable.ReadTimeGone(__instance) <= ceiling)
		{
			return;
		}

		RadiationLineTable.WriteTimeGone(__instance, ceiling);

		// Update already wrote the position from the UNCLAMPED value, so it is written again with the game's
		// own formula: up * halfHeight + down * timeGone + down * sin(timeGone * 0.66) * 3.
		var world = WorldGeneration.world;
		if (world != null) // Unity object — ==
		{
			__instance.transform.position =
				(Vector3.up * world.halfHeight) + (Vector3.down * ceiling) + (Vector3.down * Mathf.Sin(ceiling * 0.66f) * 3f);
		}
	}
}
