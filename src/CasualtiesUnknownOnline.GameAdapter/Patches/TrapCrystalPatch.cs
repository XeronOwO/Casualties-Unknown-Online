using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;
using UnityEngine;

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

	/// <summary>Teleport crystal Touched prefix — captures the body's position
	/// BEFORE CrystalTeleport.Touched runs, so the postfix can report only a
	/// REAL teleport (the method silently returns when the 1000-iteration
	/// raycast never finds a ground point, CrystalTeleport.cs:19-37).</summary>
	internal static void TeleportTouchedPrefix(object __instance, GameObject touched, out Vector2 __state)
	{
		__state = default;
		if (__instance is not CrystalEffect crystal)
		{
			return;
		}

		if (Utils.GetBody(touched, out var body) && body != null) // Unity object — ==
		{
			__state = body.transform.position;
		}
	}

	/// <summary>Teleport crystal Touched postfix — the body moved (the crystal's
	/// random ground teleport ran): report the repeatable event. The body's new
	/// position/stats ride the 20 Hz player stream; the event replays the
	/// shared observerlaugh + FlashBrief.</summary>
	internal static void TeleportTouchedPostfix(object __instance, GameObject touched, Vector2 __state)
	{
		if (__instance is not CrystalEffect crystal)
		{
			return;
		}

		if (!Utils.GetBody(touched, out var body) || body == null) // Unity object — ==
		{
			return;
		}

		if (Vector2.Distance(__state, body.transform.position) > 1f)
		{
			PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.CrystalTeleportTriggered, crystal.crystal.transform.position, 0);
		}
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

	// ---- CrystalMimic (the dispatcher-level latch observation). CrystalMimic
	// is internal, but the PUBLIC dispatchers CrystalBehaviour.OnCollisionEnter2D
	// and BuildingHit are the exact entry points that invoke every effect
	// (CrystalBehaviour.cs:74-88); observing the mimic's activated false→true
	// edge there keeps this patch fully attributed (no dynamic target) while the
	// actual spawn reports ride the generic EntitySpawned channel. ----

	/// <summary>Mimic Touched — the touch flipped the activated latch (the
	/// observerlaugh + crystalenemy spawns ran; CrystalMimic.cs:23-35).</summary>
	[HarmonyPatch(typeof(CrystalBehaviour), "OnCollisionEnter2D")]
	internal static class MimicTouchedPatch
	{
		private static void Prefix(CrystalBehaviour __instance, out bool __state) =>
			__state = CrystalMimicAccess.IsActivated(__instance);

		private static void Postfix(CrystalBehaviour __instance, bool __state)
		{
			if (__state || !CrystalMimicAccess.IsActivated(__instance))
			{
				return; // not the touch that activated the mimic (or no mimic on this crystal)
			}

			PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.CrystalMimicTriggered, __instance.transform.position, 0);
		}
	}

	/// <summary>Mimic Hit — an attack flipped the activated latch (the same
	/// observerlaugh + crystalenemy spawns; CrystalMimic.cs:38-49).</summary>
	[HarmonyPatch(typeof(CrystalBehaviour), "BuildingHit")]
	internal static class MimicHitPatch
	{
		private static void Prefix(CrystalBehaviour __instance, out bool __state) =>
			__state = CrystalMimicAccess.IsActivated(__instance);

		private static void Postfix(CrystalBehaviour __instance, bool __state)
		{
			if (__state || !CrystalMimicAccess.IsActivated(__instance))
			{
				return; // not the attack that activated the mimic (or no mimic on this crystal)
			}

			PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.CrystalMimicTriggered, __instance.transform.position, 0);
		}
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

	/// <summary>Unstable crystal StartTimer — the false→true timerStarted edge
	/// (CrystalUnstable.cs:31-37): the touch or hit just STARTED the 5 s
	/// pre-explosion ticking (talk "..!" + crystaltick sound + the Update's
	/// glow ramp and jitter follow). The prefix captures the latch, the
	/// postfix reports the rise — the ticking's true start, exactly like the
	/// mine's pressed edge. The receiver replays ONLY the ticking visual
	/// (never the latch — a remote timerStarted would make the local
	/// CrystalUnstable.Update count down and explode the crystal naturally,
	/// double-applying the world effects the Exploded event already replays).
	/// </summary>
	internal static void UnstableTimerStartPrefix(object __instance, out bool __state) =>
		__state = Traverse.Create(__instance).Field("timerStarted").GetValue<bool>();

	internal static void UnstableTimerStartPostfix(object __instance, bool __state)
	{
		if (__state || !Traverse.Create(__instance).Field("timerStarted").GetValue<bool>())
		{
			return; // not the timerStarted false → true edge
		}

		ReportCrystal(__instance, EntityEventKind.CrystalUnstableTicked);
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
