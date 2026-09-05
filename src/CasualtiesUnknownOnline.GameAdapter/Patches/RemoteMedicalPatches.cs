using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Native medical (WoundView) focus for a remote player. The game's own
/// health panel is hard-wired to the local body; while
/// <see cref="RemoteMedicalView"/> is open these patches point it at the
/// display-only body copy and block every interactive/native mutation action
/// (nap, limb-use, radial use/wear) so the remote view is strictly read-only.
/// </summary>
internal static class RemoteMedicalPatches
{
	[HarmonyPatch(typeof(WoundView), "Update")]
	internal static class RemoteMedicalWoundViewBodyPatch
	{
		private static void Prefix(WoundView __instance)
		{
			if (!RemoteMedicalView.IsOpen || RemoteMedicalView.DisplayBody is not { } display)
			{
				return;
			}

			__instance.body = display;
		}
	}

	[HarmonyPatch(typeof(WoundView), "TakeANap")]
	internal static class RemoteMedicalWoundViewNoNapPatch
	{
		private static bool Prefix() => !RemoteMedicalView.IsOpen;
	}

	[HarmonyPatch(typeof(WoundViewLimb), "OnPointerEnter")]
	internal static class RemoteMedicalWoundViewLimbHoverPatch
	{
		private static bool Prefix(WoundViewLimb __instance)
		{
			if (!RemoteMedicalView.IsOpen)
			{
				return true;
			}

			if (RemoteMedicalView.DisplayBody is { } display
				&& display.limbs.Length > __instance.limb
				&& display.limbs[__instance.limb] != null // Unity object — ==
				&& !display.limbs[__instance.limb].dismembered)
			{
				__instance.woundview.limbLookingAt = __instance.limb;
				__instance.woundview.limbImageFlash[__instance.limb] = 1f;
			}

			return false;
		}
	}

	[HarmonyPatch(typeof(PlayerCamera), "TryPerformRadialAction")]
	internal static class RemoteMedicalBlockRadialActionPatch
	{
		private static bool Prefix(ref bool __result)
		{
			if (RemoteMedicalView.IsOpen)
			{
				__result = false;
				return false;
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(PlayerCamera), "TryPerformSpecialUIAction")]
	internal static class RemoteMedicalBlockSpecialActionPatch
	{
		private static bool Prefix(ref bool __result)
		{
			if (RemoteMedicalView.IsOpen)
			{
				__result = false;
				return false;
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(PlayerCamera), "ToggleWoundView")]
	internal static class RemoteMedicalToggleWoundViewCleanupPatch
	{
		private static void Postfix(PlayerCamera __instance)
		{
			if (!RemoteMedicalView.IsOpen)
			{
				return;
			}

			if (__instance.woundView == null || !__instance.woundView.activeSelf) // Unity object — ==
			{
				RemoteMedicalView.Close();
			}
		}
	}
}
