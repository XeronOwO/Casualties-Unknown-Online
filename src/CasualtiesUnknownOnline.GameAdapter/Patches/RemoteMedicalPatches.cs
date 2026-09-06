using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;
using UnityEngine.EventSystems;

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

		private static void Postfix(WoundView __instance)
		{
			if (!RemoteMedicalView.IsOpen)
			{
				return;
			}

			// The native UpdateView sets napbutton.interactable from the
			// display body's canTakeNap every frame. In remote focus the nap
			// action is already blocked by the TakeANap prefix, but the button
			// must also be visibly disabled so the viewer is never invited to
			// sleep on another player's display body.
			if (__instance.napbutton != null) // Unity object — ==
			{
				__instance.napbutton.interactable = false;
			}
		}
	}

	[HarmonyPatch(typeof(ECGVisualizer), "get_body")]
	internal static class RemoteMedicalEcgBodyPatch
	{
		private static void Prefix(ECGVisualizer __instance, ref Body __result)
		{
			if (!RemoteMedicalView.IsOpen || RemoteMedicalView.DisplayBody is not { } display)
			{
				return;
			}

			// Only redirect the ECG inside the active native WoundView panel.
			// The same getter is used by the consciousness overlay and the
			// manual defibrillator minigame; those must keep drawing the local
			// body even while the remote medical view is open.
			if (WoundView.view == null // Unity object — ==
				|| !__instance.transform.IsChildOf(WoundView.view.transform))
			{
				return;
			}

			// The native ECG waveform is hard-wired to PlayerCamera.main.body
			// (ECGVisualizer.cs:10-16). While the remote WoundView is open it
			// would keep drawing the viewer's own heartbeat next to the remote
			// readout; redirect it to the same display body the panel reads.
			__result = display;
		}
	}

	[HarmonyPatch(typeof(MoodleManager), "UpdateMoodles")]
	internal static class RemoteMedicalMoodleBodyPatch
	{
		private static void Prefix(MoodleManager __instance, out Body? __state)
		{
			__state = null;
			if (!RemoteMedicalView.IsOpen || RemoteMedicalView.DisplayBody is not { } display)
			{
				return;
			}

			var traverse = Traverse.Create(__instance);
			__state = traverse.Field("body").GetValue<Body>();
			traverse.Field("body").SetValue(display);
		}

		private static void Postfix(MoodleManager __instance, Body? __state)
		{
			if (__state == null)
			{
				return;
			}

			Traverse.Create(__instance).Field("body").SetValue(__state);
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

	[HarmonyPatch(typeof(PlayerCamera), "WoundSpecialAction")]
	internal static class RemoteMedicalBlockWoundSpecialActionPatch
	{
		private static bool Prefix(PlayerCamera __instance)
		{
			if (!RemoteMedicalView.IsOpen)
			{
				return true;
			}

			// The one allowed remote special action: shrapnel removal. It is
			// routed through the shared host-authoritative shrapnel session and
			// started on the display body; every other special stays read-only.
			if (__instance.selectedLimb != null // Unity object — ==
				&& __instance.selectedLimb.hasShrapnel
				&& PatchBridge.Impl?.TryStartRemoteShrapnelSpecial(__instance.selectedLimb) == true)
			{
				return false;
			}

			return false;
		}
	}

	[HarmonyPatch(typeof(PlayerCamera), "ApplyWoundItem")]
	internal static class RemoteMedicalBlockApplyWoundItemPatch
	{
		private static bool Prefix() => !RemoteMedicalView.IsOpen;
	}

	[HarmonyPatch(typeof(PlayerCamera), "TryPerformSpecialUIAction")]
	internal static class RemoteMedicalBlockSpecialActionPatch
	{
		private static bool Prefix(PlayerCamera __instance, RaycastResult hit, ref bool __result)
		{
			if (!RemoteMedicalView.IsOpen)
			{
				return true;
			}

			// The one allowed remote-medical treatment gesture: the acting
			// player drops a local medical item onto the displayed body's limb.
			// It is routed through the host-authoritative heal/use request path;
			// the native display-only body is never mutated.
			var woundView = __instance.woundView != null // Unity object — ==
				? __instance.woundView.GetComponent<WoundView>()
				: null; // Unity object — ==
			if (hit.gameObject.GetComponent<WoundViewLimb>() != null // Unity object — ==
				&& __instance.dragItem != null // Unity object — ==
				&& PatchBridge.Impl?.TryHandleRemoteMedicalLimbUse(
					__instance.dragItem,
					woundView != null ? woundView.limbLookingAt : -1) == true)
			{
				__result = true;
				return false;
			}

			// Every other native special/limb action stays read-only while the
			// remote focus is open.
			__result = false;
			return false;
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

	[HarmonyPatch(typeof(MinigameBase), "EndMinigame")]
	internal static class RemoteMedicalSyringeEndPatch
	{
		private static void Postfix()
		{
			RemoteMedicalOperationHandler.CompleteActiveShrapnelUse();
			RemoteMedicalOperationHandler.CompleteActiveSyringeUse();
		}
	}
}
