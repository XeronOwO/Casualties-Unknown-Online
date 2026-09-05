using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Two duties on <c>PlayerCamera.HandleInput</c>:
/// 1. While the CUO Online UI modal window is open, the game's native
///    <c>PlayerCamera.HandleInput</c> must not process keyboard input — most
///    importantly the pause/ESC key would otherwise toggle the pause menu behind
///    the Online UI. The modal is left active for one frame after an ESC-closing
///    CUO surface closes (see <c>CuoEscCloseSuppression</c>), so even when OnGUI
///    is observed before Update the same ESC cannot reach the game's pause path.
/// 2. The ragdoll-key input branch (PlayerCamera.cs:958-961) is the only
///    <c>Body.Ragdoll</c> call in HandleInput that belongs to a player-triggered
///    collapse. The prefix records the local body's standing flag; the postfix
///    observes the standing → collapsing transition and reports the one-shot
///    presentation event for the peers' clones. External ragdoll sources (traps,
///    enemy attacks, cross-player push, timed medicine) are deliberately not
///    reported here — those ride their own dedicated event/state chains.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleInput")]
internal static class PlayerCameraHandleInputPatch
{
	private sealed class RagdollObservation
	{
		internal Body? Body;
		internal bool WasStanding;
	}

	private static bool Prefix(PlayerCamera __instance, out RagdollObservation __state)
	{
		if (PatchBridge.Impl is { IsOnlineUiModalOpen: true })
		{
			__state = null!;
			return false;
		}

		var body = __instance.body; // Unity object — ==
		__state = new RagdollObservation
		{
			Body = body,
			WasStanding = body != null && body.standing,
		};
		return true;
	}

	private static void Postfix(PlayerCamera __instance, RagdollObservation __state)
	{
		if (__state == null)
		{
			return;
		}

		var body = __state.Body;
		if (body == null || !__state.WasStanding || body.standing) // Unity object — ==
		{
			return;
		}

		PatchBridge.Impl?.OnCharacterRagdoll((Vector2)body.transform.position);
	}
}
