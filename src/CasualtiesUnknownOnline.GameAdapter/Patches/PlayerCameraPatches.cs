using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Guest-side input interception. In a live guest session, HandleInput is
/// replaced: movement/jump/look keys are read and submitted to the session
/// instead of driving the local body. World-interaction inputs (attack, pickup,
/// use, throw, ragdoll) stay swallowed — Phase 3 syncs them and they must not
/// mutate local world state in the meantime. View-only UI keys (pause,
/// inventory, wound view) still work locally.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleInput")]
internal static class PlayerCameraHandleInputPatch
{
	private static bool Prefix(PlayerCamera __instance)
	{
		var adapter = GameAdapter.Instance;
		if (adapter is null || !adapter.IsGuestMode)
		{
			return true; // host or single-player: original behavior
		}

		var moveX = (Input.GetKey(KeyBinds.GetBind("right")) ? 1f : 0f)
			- (Input.GetKey(KeyBinds.GetBind("left")) ? 1f : 0f);
		var moveY = (Input.GetKey(KeyBinds.GetBind("up")) ? 1f : 0f)
			- (Input.GetKey(KeyBinds.GetBind("down")) ? 1f : 0f);
		var jump = Input.GetKeyDown(KeyBinds.GetBind("jump"));
		var crouch = Input.GetKey(KeyBinds.GetBind("down"));

		// Local simulation: the guest's body runs full physics with its own
		// input (instant feel); the same input goes to the host for validation.
		__instance.body.moveDir = new Vector2(moveX, moveY);
		__instance.body.crouching = crouch;
		if (jump && __instance.body.standing && __instance.body.conscious)
		{
			__instance.body.Jump();
		}

		adapter.SubmitGuestInput(moveX, moveY, jump, crouch);

		// View-only UI keys: replicate the original handling for the panels the
		// guest is allowed to see. World interactions are intentionally absent.
		if (Input.GetKeyDown(KeyBinds.GetBind("pause")))
		{
			if (PauseHandler.paused)
			{
				PauseHandler.TogglePause();
			}
			else if (__instance.woundView.activeSelf)
			{
				__instance.ToggleWoundView(true);
			}
			else if (__instance.craftingPanel.activeSelf)
			{
				__instance.OpenCraftScreen();
			}
			else if (__instance.radialOpen)
			{
				__instance.radialOpen = false;
			}
			else
			{
				PauseHandler.TogglePause();
			}
		}

		if (Input.GetKeyDown(KeyBinds.GetBind("toggleinventory")))
		{
			__instance.radialOpen = !__instance.radialOpen;
		}

		if (Input.GetKeyDown(KeyBinds.GetBind("woundview")))
		{
			__instance.ToggleWoundView(true);
		}

		return false;
	}
}
