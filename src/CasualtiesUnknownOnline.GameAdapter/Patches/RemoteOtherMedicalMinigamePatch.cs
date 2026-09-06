using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Native minigame patches for the Stage 3 remote-medical actions. They do not
/// block the native minigame presentation; they only report semantic events to
/// the host-authoritative <see cref="RemoteOtherMedicalOperationHandler"/>.
/// </summary>
internal static class RemoteOtherMedicalMinigamePatch
{
	internal static void Reset()
	{
		AmputationPhysicsUpdatePatch.LastCut = -1f;
		AedUpdatePatch.LastState = -1;
	}

	[HarmonyPatch(typeof(DislocationMinigame), "CheckForHit")]
	internal static class DislocationCheckForHitPatch
	{
		private static void Prefix(DislocationMinigame __instance, List<RaycastResult> uiCasts)
		{
			if (!RemoteOtherMedicalOperationHandler.IsActiveDislocation(__instance))
			{
				return;
			}

			var limb = Traverse.Create(__instance).Field("limb").GetValue<Limb>();
			if (limb == null)
			{
				return;
			}

			if (limb.body.averagePain > 75f
				|| !Minigame.game.handClicking
				|| Minigame.game.handVelocity.magnitude < 4f)
			{
				return;
			}

			var bone = Traverse.Create(__instance).Field("bone").GetValue<RectTransform>();
			if (bone == null)
			{
				return;
			}

			foreach (var raycastResult in uiCasts)
			{
				if (raycastResult.gameObject == bone.gameObject)
				{
					RemoteOtherMedicalOperationHandler.ReportDislocationHit(__instance.hasWrench);
					break;
				}
			}
		}
	}

	[HarmonyPatch(typeof(AmputationMinigame), "PhysicsUpdate")]
	internal static class AmputationPhysicsUpdatePatch
	{
		internal static float LastCut = -1f;

		private static void Prefix(AmputationMinigame __instance)
		{
			if (RemoteOtherMedicalOperationHandler.IsActiveAmputation(__instance) && LastCut < 0f)
			{
				LastCut = __instance.cutProgress;
			}
		}

		private static void Postfix(AmputationMinigame __instance)
		{
			if (!RemoteOtherMedicalOperationHandler.IsActiveAmputation(__instance) || LastCut < 0f)
			{
				return;
			}

			var delta = __instance.cutProgress - LastCut;
			if (delta > 0.001f)
			{
				RemoteOtherMedicalOperationHandler.ReportAmputationCut(delta);
				LastCut = __instance.cutProgress;
			}
		}
	}

	[HarmonyPatch(typeof(AEDMinigame), "Update")]
	internal static class AedUpdatePatch
	{
		internal static int LastState = -1;

		private static void Postfix(AEDMinigame __instance)
		{
			if (!RemoteOtherMedicalOperationHandler.IsActiveAed(__instance))
			{
				return;
			}

			var state = Traverse.Create(__instance).Field("state").GetValue<int>();
			if (LastState < 0)
			{
				LastState = state;
				return;
			}

			if (state == 2 && LastState != 2)
			{
				RemoteOtherMedicalOperationHandler.ReportAedAnalyze();
			}
			else if (state == 4 && LastState != 4)
			{
				RemoteOtherMedicalOperationHandler.ReportAedShock();
			}

			LastState = state;
		}
	}

	[HarmonyPatch(typeof(Item), "Defibrillate")]
	internal static class ManualDefibShockPatch
	{
		private static void Prefix(Item __instance, Item.DefibInfo info)
		{
			if (!RemoteOtherMedicalOperationHandler.IsActiveManualDefib()
				|| MinigameBase.main == null // Unity object — ==
				|| MinigameBase.main.currentMinigame is not ManualDefibMinigame manual)
			{
				return;
			}

			var charge = Traverse.Create(manual).Field("currentCharge").GetValue<float>();
			RemoteOtherMedicalOperationHandler.ReportManualShock(charge);
		}
	}
}
