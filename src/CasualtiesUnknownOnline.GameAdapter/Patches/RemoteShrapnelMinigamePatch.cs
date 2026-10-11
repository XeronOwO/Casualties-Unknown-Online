using System;
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
	internal static void ResetLastHeld()
	{
		UpdatePatch._lastHeld = -1;
		UpdatePatch._removalReportedFor = -1;
	}

	[HarmonyPatch(typeof(ShrapnelMinigame), "Update")]
	internal static class UpdatePatch
	{
		internal static int _lastHeld = -1;
		internal static int _removalReportedFor = -1;

		private static void Prefix(ShrapnelMinigame __instance, List<RaycastResult> uiCasts)
		{
			if (!RemoteMedicalView.IsOpen || !RemoteMedicalOperationHandler.IsActiveShrapnelMinigame(__instance))
			{
				return;
			}

			if (RemoteMedicalOperationHandler.IsObserverShrapnelMinigame(__instance))
			{
				// An observer watches the authoritative piece positions without
				// participating; suppress the native grab/click input.
				MinigameBase.main.handStartedClicking = false;
				MinigameBase.main.handStoppedClicking = false;
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
					var isNewGrab = _lastHeld != index;
					var isRemovalTransition = position.y >= 35f && _removalReportedFor != index;
					if (isRemovalTransition)
					{
						_removalReportedFor = index;
					}

					RemoteMedicalOperationHandler.ReportShrapnelUpdate(new ShrapnelPieceUpdate
					{
						PieceIndex = index,
						X = position.x,
						Y = position.y,
						Grabbed = true,
						OwnershipChange = isNewGrab || isRemovalTransition,
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
					OwnershipChange = true,
				});
				_lastHeld = -1;
			}
		}
	}

	[HarmonyPatch(typeof(ShrapnelMinigame), "BreakGrasp")]
	internal static class BreakGraspPatch
	{
		/// <summary>
		/// Opens the derived window around the native draw: the slip both machines derive their random
		/// state from, plus the displayed limb fields the call is about to move. Null when this
		/// minigame is not the shared session's own operator copy, in which case the native call runs
		/// unwrapped.
		/// </summary>
		private static void Prefix(ShrapnelMinigame __instance, out RemoteShrapnelGripSlip? __state)
		{
			__state = null;
			if (!RemoteMedicalView.IsOpen || !RemoteMedicalOperationHandler.IsActiveShrapnelMinigame(__instance))
			{
				return;
			}

			__state = RemoteMedicalOperationHandler.TryBeginGripSlip(__instance);
		}

		private static void Postfix(RemoteShrapnelGripSlip? __state)
		{
			if (__state is null)
			{
				return;
			}

			// Read the displayed limb BEFORE the window closes: the native call wrote these values
			// there, and they are what this client's screen is showing.
			var values = __state.ReadValues();
			__state.Close();
			RemoteMedicalOperationHandler.ReportShrapnelUpdate(new ShrapnelPieceUpdate
			{
				PieceIndex = __state.PieceIndex,
				BreakGrasp = true,
				OwnershipChange = true,
				GripSlipNonce = __state.Nonce,
				GripSlipValues = values,
			});
		}

		/// <summary>
		/// The native call threw, so the postfix never ran and the slip is still holding the engine
		/// stream. Put it back. The slip is NOT reported: half-applied damage is not a slip the
		/// authority should commit, and the authority's own draw never ran either.
		/// </summary>
		private static Exception? Finalizer(Exception? __exception, RemoteShrapnelGripSlip? __state)
		{
			__state?.Close();
			return __exception;
		}
	}
}
