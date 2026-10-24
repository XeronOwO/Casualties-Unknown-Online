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
		// The member's rate half: the native advance used THIS client's body's consciousness factor, so a member continues
		// from the host's absolute state at the host's speed instead (the user's 2026-10-24 finding: the descent looked
		// wrong on a member's screen because it ran at its own body's rate and snapped back every resend).
		if (PatchBridge.Impl?.RadiationLineGuestTimeGone is { } guestValue)
		{
			RadiationLineTable.WriteTimeGone(__instance, guestValue);
			WritePosition(__instance, guestValue);
		}

		if (PatchBridge.Impl?.RadiationLineCeiling is not { } ceiling)
		{
			return;
		}

		if (RadiationLineTable.ReadTimeGone(__instance) <= ceiling)
		{
			return;
		}

		RadiationLineTable.WriteTimeGone(__instance, ceiling);

		// Update already wrote the position from the UNCLAMPED value (or the guest correction did), so it is written
		// again with the game's own formula: up * halfHeight + down * timeGone + down * sin(timeGone * 0.66) * 3.
		WritePosition(__instance, ceiling);
	}

	/// <summary>The game's own position formula (<c>RadiationLine.cs</c>), for a value written after the native pass.</summary>
	private static void WritePosition(RadiationLine line, float timeGone)
	{
		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return;
		}

		line.transform.position =
			(Vector3.up * world.halfHeight) + (Vector3.down * timeGone) + (Vector3.down * Mathf.Sin(timeGone * 0.66f) * 3f);
	}
}
