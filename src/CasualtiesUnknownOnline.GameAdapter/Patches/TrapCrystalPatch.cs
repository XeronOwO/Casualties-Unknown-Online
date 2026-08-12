using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Fragile crystal → CrystalFragileBroken (one-shot): an item or body touched
/// it and its health dropped to 0 (CrystalFragile.cs:14-21 — the crystal
/// shatters; the drops roll on the triggering side). CrystalFragile is
/// INTERNAL to the game assembly (unreferencable at compile time) and its
/// Touched OVERRIDES the public base (a base-class patch cannot intercept the
/// CLR dispatch), so this patch is installed DYNAMICALLY by the adapter
/// (GameAdapter.InstallDynamicPatches) on the reflected type. The postfix
/// reads the health transition — the only Touched path that writes health = 0
/// for the fragile variant is its break.
/// </summary>
internal static class TrapCrystalPatch
{
	/// <summary>Electric crystal Shock postfix — installed dynamically with
	/// Postfix (CrystalElectric is internal too). The zap + shake replay the
	/// visible state; the electric damage hits the triggering side's body.</summary>
	internal static void ElectricShockPostfix(object __instance)
	{
		if (__instance is not CrystalEffect crystal)
		{
			return;
		}

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.CrystalElectricShocked, crystal.crystal.transform.position, 0);
	}

	internal static void Postfix(object __instance)
	{
		if (__instance is not CrystalEffect crystal)
		{
			return;
		}

		if (crystal.crystal.build.health >= 0.5f)
		{
			return; // not broken by this touch (the touch conditions failed)
		}

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.CrystalFragileBroken, crystal.crystal.transform.position, 0);
	}

	// ---- The phase-B crystal family (all INTERNAL classes — installed
	// dynamically by GameAdapter.InstallDynamicPatches, same rule as the two
	// above: an override cannot be intercepted through the base class). The
	// one-shot latch (timerStarted / activated) is the report trigger — the
	// prefix captures it, the postfix detects the rise. ----

	/// <summary>Unstable crystal Update — the timer crossed 5 s: the crystal
	/// exploded (CrystalUnstable.cs:40-64). The prefix captures the timer, the
	/// postfix reports the rise across the 5 s boundary — the explosion moment
	/// itself, exactly like the mine's exploded rise.</summary>
	internal static void UnstableUpdatePrefix(object __instance, out float __state) =>
		__state = Traverse.Create(__instance).Field("timer").GetValue<float>();

	internal static void UnstableUpdatePostfix(object __instance, float __state)
	{
		if (__state > 5f || Traverse.Create(__instance).Field("timer").GetValue<float>() <= 5f)
		{
			return; // not the timer crossing the 5 s explosion boundary
		}

		ReportCrystal(__instance, EntityEventKind.CrystalUnstableExploded);
	}

	/// <summary>Metamorphic crystal Touched — the activated latch rose (the touch
	/// applied: FlashBrief + death + drops; CrystalMetamorphic.cs:16-35).</summary>
	internal static void MetamorphicTouchedPrefix(object __instance, out bool __state) =>
		__state = Traverse.Create(__instance).Field("activated").GetValue<bool>();

	internal static void MetamorphicTouchedPostfix(object __instance, bool __state)
	{
		if (__state || !Traverse.Create(__instance).Field("activated").GetValue<bool>())
		{
			return; // not the touch that activated it
		}

		ReportCrystal(__instance, EntityEventKind.CrystalMetamorphicTriggered);
	}

	/// <summary>Shy crystal Touched — the activated latch rose (the position swap
	/// happened; CrystalShy.cs:8-33).</summary>
	internal static void ShyTouchedPrefix(object __instance, out bool __state) =>
		__state = Traverse.Create(__instance).Field("activated").GetValue<bool>();

	internal static void ShyTouchedPostfix(object __instance, bool __state)
	{
		if (__state || !Traverse.Create(__instance).Field("activated").GetValue<bool>())
		{
			return; // not the touch that swapped it
		}

		ReportCrystal(__instance, EntityEventKind.CrystalShySwapped);
	}

	/// <summary>EMP crystal TryEMP — the activated latch rose (the EMP fired;
	/// CrystalEMP.cs:14-35).</summary>
	internal static void EmpTryEMPPrefix(object __instance, out bool __state) =>
		__state = Traverse.Create(__instance).Field("activated").GetValue<bool>();

	internal static void EmpTryEMPPostfix(object __instance, bool __state)
	{
		if (__state || !Traverse.Create(__instance).Field("activated").GetValue<bool>())
		{
			return; // not the TryEMP that activated it
		}

		ReportCrystal(__instance, EntityEventKind.CrystalEMPActivated);
	}

	/// <summary>The position-keyed report — the crystal's transform is the identity.</summary>
	private static void ReportCrystal(object instance, EntityEventKind kind)
	{
		if (instance is not CrystalEffect crystal)
		{
			return;
		}

		PatchBridge.Impl?.OnTrapTriggered(kind, crystal.crystal.transform.position, 0);
	}
}
