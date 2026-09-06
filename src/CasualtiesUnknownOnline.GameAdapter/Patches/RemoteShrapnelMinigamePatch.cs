using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Remote shrapnel minigame adapter. While the operator's local
/// <see cref="ShrapnelMinigame"/> is a shared remote session, the update patch
/// keeps the native UI honest about host-side leases (non-owner pieces are
/// force-ungrabbed) and reports the local operator's piece grabs/moves/releases
/// to the host. The break-grasp patch reports the failure so the host can
/// apply native damage semantics once.
/// </summary>
internal static class RemoteShrapnelMinigamePatch
{
	[HarmonyPatch(typeof(ShrapnelMinigame), "Update")]
	internal static class UpdatePatch
	{
		private static int _lastHeld = -1;

		private static void Prefix(ShrapnelMinigame __instance, List<RaycastResult> uiCasts)
		{
			if (!RemoteMedicalView.IsOpen || !RemoteMedicalOperationHandler.IsActiveShrapnelMinigame(__instance))
			{
				return;
			}

			var traverse = Traverse.Create(__instance);
			var objects = traverse.Field("objects").GetValue<List<RectTransform>>();
			var held = traverse.Field("currentlyHeld").GetValue<RectTransform>();
			if (objects is null || held == null)
			{
				return;
			}

			var index = objects.IndexOf(held);
			if (index >= 0 && RemoteMedicalOperationHandler.IsShrapnelPieceOwnedByOther(index))
			{
				MinigameBase.main.ForceUngrab();
				traverse.Field("currentlyHeld").SetValue(null);
			}
		}

		private static void Postfix(ShrapnelMinigame __instance, List<RaycastResult> uiCasts)
		{
			if (!RemoteMedicalView.IsOpen || !RemoteMedicalOperationHandler.IsActiveShrapnelMinigame(__instance))
			{
				return;
			}

			var traverse = Traverse.Create(__instance);
			var objects = traverse.Field("objects").GetValue<List<RectTransform>>();
			var held = traverse.Field("currentlyHeld").GetValue<RectTransform>();
			if (objects is null)
			{
				return;
			}

			if (held != null)
			{
				var index = objects.IndexOf(held);
				if (index >= 0)
				{
					var position = held.anchoredPosition;
					RemoteMedicalOperationHandler.ReportShrapnelUpdate(new ShrapnelPieceUpdate
					{
						PieceIndex = index,
						X = position.x,
						Y = position.y,
						Grabbed = true,
					});
					_lastHeld = index;
				}
			}
			else if (_lastHeld >= 0)
			{
				RemoteMedicalOperationHandler.ReportShrapnelUpdate(new ShrapnelPieceUpdate
				{
					PieceIndex = _lastHeld,
					Released = true,
				});
				_lastHeld = -1;
			}
		}
	}

	[HarmonyPatch(typeof(ShrapnelMinigame), "BreakGrasp")]
	internal static class BreakGraspPatch
	{
		private static void Prefix(ShrapnelMinigame __instance, out int __state)
		{
			__state = -1;
			if (!RemoteMedicalView.IsOpen || !RemoteMedicalOperationHandler.IsActiveShrapnelMinigame(__instance))
			{
				return;
			}

			var objects = Traverse.Create(__instance).Field("objects").GetValue<List<RectTransform>>();
			var held = Traverse.Create(__instance).Field("currentlyHeld").GetValue<RectTransform>();
			if (objects is not null && held != null)
			{
				__state = objects.IndexOf(held);
			}
		}

		private static void Postfix(ShrapnelMinigame __instance, int __state)
		{
			if (!RemoteMedicalView.IsOpen
				|| __state < 0
				|| !RemoteMedicalOperationHandler.IsActiveShrapnelMinigame(__instance))
			{
				return;
			}

			RemoteMedicalOperationHandler.ReportShrapnelUpdate(new ShrapnelPieceUpdate
			{
				PieceIndex = __state,
				BreakGrasp = true,
			});
		}
	}
}
