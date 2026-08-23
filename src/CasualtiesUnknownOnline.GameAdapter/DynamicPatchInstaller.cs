using System.Reflection;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using HarmonyLib;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The dynamic patches: targets whose types are INTERNAL to the game assembly
/// (no compile-time reference possible) — reflect the type and patch the
/// method directly. The patch methods live in the Patches namespace
/// (TrapCrystalPatch / CrystalDrippingPatch). Split out of GameAdapter at the
/// 600-line gate — "install the dynamic patches" is one responsibility.
/// </summary>
internal static class DynamicPatchInstaller
{
	internal static void Install(Harmony harmony, ILogger log)
	{
		var fragileType = typeof(CrystalEffect).Assembly.GetType("CrystalFragile");
		if (fragileType == null)
		{
			log.LogError("Dynamic patch target CrystalFragile not found — the fragile-crystal break sync is off.");
			return;
		}

		var touched = fragileType.GetMethod("Touched", BindingFlags.Public | BindingFlags.Instance);
		if (touched != null)
		{
			harmony.Patch(touched, postfix: new HarmonyMethod(typeof(TrapCrystalPatch).GetMethod(
				nameof(TrapCrystalPatch.Postfix), BindingFlags.Static | BindingFlags.NonPublic)));
		}
		else
		{
			log.LogError("Dynamic patch method CrystalFragile.Touched not found — the fragile-crystal break sync is off.");
		}

		var electricType = typeof(CrystalEffect).Assembly.GetType("CrystalElectric");
		if (electricType == null)
		{
			log.LogError("Dynamic patch target CrystalElectric not found — the electric-crystal shock sync is off.");
			return;
		}

		var shock = electricType.GetMethod("Shock", BindingFlags.Public | BindingFlags.Instance);
		if (shock != null)
		{
			harmony.Patch(shock, postfix: new HarmonyMethod(typeof(TrapCrystalPatch).GetMethod(
				nameof(TrapCrystalPatch.ElectricShockPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
		}
		else
		{
			log.LogError("Dynamic patch method CrystalElectric.Shock not found — the electric-crystal shock sync is off.");
		}

		// CrystalTeleport (internal — the Touched override cannot be intercepted
		// through the base class): the body moved on a real ground-teleport, so
		// the repeatable laugh/flash event reports; the teleported body itself
		// rides the 20 Hz player stream.
		InstallCrystalFamilyPatch(harmony, log, "CrystalTeleport", "Touched", "TeleportTouchedPrefix", "TeleportTouchedPostfix");

		// CrystalDripping (internal — the drip's fluid writes are the host's, #129).
		var drippingType = typeof(CrystalEffect).Assembly.GetType("CrystalDripping");
		var dripUpdate = drippingType?.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance);
		if (dripUpdate != null)
		{
			harmony.Patch(dripUpdate, prefix: new HarmonyMethod(typeof(CrystalDrippingPatch).GetMethod(
				nameof(CrystalDrippingPatch.Prefix), BindingFlags.Static | BindingFlags.NonPublic)));
		}
		else
		{
			log.LogError("Dynamic patch target CrystalDripping not found — the guest-side drip suppression is off.");
		}

		// The phase-B crystal family (internal classes — the same dynamic rule):
		// the unstable crystal's 5 s explosion, the metamorphic touch, the shy
		// swap and the EMP. Each installs the latch-rise prefix/postfix pair.
		InstallCrystalFamilyPatch(harmony, log, "CrystalUnstable", "Update", "UnstableUpdatePrefix", "UnstableUpdatePostfix");
		InstallCrystalFamilyPatch(harmony, log, "CrystalMetamorphic", "Touched", "MetamorphicTouchedPrefix", "MetamorphicTouchedPostfix");
		InstallCrystalFamilyPatch(harmony, log, "CrystalShy", "Touched", "ShyTouchedPrefix", "ShyTouchedPostfix");
		InstallCrystalFamilyPatch(harmony, log, "CrystalEMP", "TryEMP", "EmpTryEMPPrefix", "EmpTryEMPPostfix");

		// The unstable crystal's ticking START — StartTimer is PRIVATE (the
		// false→true `timerStarted` edge that the Update patch cannot see: the
		// latch is set inside Touched/Hit→StartTimer, and Update only runs
		// once it is already true). A separate NonPublic install.
		var unstableTimerType = typeof(CrystalEffect).Assembly.GetType("CrystalUnstable");
		var startTimer = unstableTimerType?.GetMethod("StartTimer", BindingFlags.NonPublic | BindingFlags.Instance);
		if (startTimer != null)
		{
			harmony.Patch(startTimer,
				prefix: new HarmonyMethod(typeof(TrapCrystalPatch).GetMethod(
					nameof(TrapCrystalPatch.UnstableTimerStartPrefix), BindingFlags.Static | BindingFlags.NonPublic)),
				postfix: new HarmonyMethod(typeof(TrapCrystalPatch).GetMethod(
					nameof(TrapCrystalPatch.UnstableTimerStartPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
		}
		else
		{
			log.LogError("Dynamic patch method CrystalUnstable.StartTimer not found — the crystal ticking visual sync is off.");
		}
	}

	/// <summary>Install a crystal-family latch-rise pair (prefix + postfix) onto
	/// an internal game type's method — the phase-B family helper.</summary>
	private static void InstallCrystalFamilyPatch(Harmony harmony, ILogger log, string typeName, string methodName, string prefixName, string postfixName)
	{
		var type = typeof(CrystalEffect).Assembly.GetType(typeName);
		var method = type?.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
		if (method == null)
		{
			log.LogError("Dynamic patch target {Type}.{Method} not found — the crystal sync is off.", typeName, methodName);
			return;
		}

		var prefix = typeof(TrapCrystalPatch).GetMethod(prefixName, BindingFlags.Static | BindingFlags.NonPublic);
		var postfix = typeof(TrapCrystalPatch).GetMethod(postfixName, BindingFlags.Static | BindingFlags.NonPublic);
		harmony.Patch(method, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
	}
}
