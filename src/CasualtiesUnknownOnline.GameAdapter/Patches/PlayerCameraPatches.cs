using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Guest-side input interception. In a live guest session, HandleInput is
/// replaced: movement/jump keys are read and submitted to the session instead
/// of driving the local body, and the rest of the local interaction logic is
/// skipped (Phase 1 syncs movement only — inventory/combat/interaction land in
/// Phase 3 and must not mutate local world state in the meantime).
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

		adapter.SubmitGuestInput(moveX, moveY, jump, crouch);
		__instance.body.moveDir = Vector2.zero;
		return false;
	}
}
