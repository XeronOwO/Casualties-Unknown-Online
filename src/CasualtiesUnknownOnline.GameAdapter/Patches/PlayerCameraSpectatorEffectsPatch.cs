using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The spectator's screen effects are the WATCHED body's, never the local one. <c>PlayerCamera.HandleUnconsciousScreen</c>
/// is the one writer of the screen presentation — the black amount, the energy/ECG fill and colour, the consciousness
/// vignette and the death fade — and while the mode is on it must show what the followed player is living through
/// instead of what the local body (which is standing somewhere else) would produce. The prefix recomputes the whole
/// visual set from the watched body (the effect family, not only the two values the old patch carried) and deliberately
/// leaves the local time scale and wound view alone. On exit the game's own code recomputes everything from the local
/// body.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleUnconsciousScreen")]
internal static class PlayerCameraSpectatorEffectsPatch
{
	private static bool Prefix(PlayerCamera __instance)
	{
		if (!SpectatorPresentationGate.IsActive)
		{
			return true;
		}

		if (SpectatorPresentationGate.HudBody is not { } watched || watched == null) // Unity object — ==
		{
			__instance.blackAmount = 0f;
			if (__instance.consciousnessVignette != null) // Unity object — ==
			{
				__instance.consciousnessVignette.color = Color.clear;
			}

			return false;
		}

		MirrorUnconsciousScreen(__instance, watched);
		return false;
	}

	/// <summary>
	/// The native method writes six player-visible things from <c>this.body</c>: the fade amount, the sleep/energy
	/// fill, the ECG activation and colour, the consciousness text colour and the vignette. Pointing only the fade
	/// and the vignette at the watched body left the rest showing the local body's stale state, so the whole visual
	/// set is recomputed here from the watched body. The transition and time-scale branches are deliberately not
	/// run: the spectator's own clock and wound view must not become the watched player's.
	/// </summary>
	private static void MirrorUnconsciousScreen(PlayerCamera camera, Body watched)
	{
		var cutscene = WorldGeneration.world != null && WorldGeneration.world.cutsceneActive;
		var unconsciousBlack = PlayerCamera.GetUnconsciousBlack();
		var brainDying = watched.brainDying;
		var fill = watched.alive ? watched.energy * 0.01f : 0f;
		var consciousness = cutscene ? 100f : watched.consciousness;
		var flag = consciousness < 20f;
		var color = Color.white;

		if (flag && (watched.alive || !camera.didDeathScreen))
		{
			camera.blackAmount = Mathf.MoveTowards(camera.blackAmount, watched.alive ? unconsciousBlack : 1f, Time.unscaledDeltaTime * 2f);
			if (brainDying)
			{
				fill = watched.brainHealth * 0.01f;
				color = Color.red;
			}
		}
		else
		{
			camera.blackAmount = Mathf.MoveTowards(
				camera.blackAmount,
				watched.alive ? Mathf.Clamp01((0.7f - consciousness * 0.01f) * 2.1f) : 0.25f,
				Time.deltaTime * (watched.alive ? 0.8f : 0.045f));
			if (camera.blackAmount > unconsciousBlack)
			{
				camera.blackAmount = unconsciousBlack;
			}
		}

		if (camera.blackAmount >= unconsciousBlack && flag)
		{
			camera.timeHasBeenBlack = Mathf.MoveTowards(camera.timeHasBeenBlack, 4f, Time.unscaledDeltaTime);
		}
		else
		{
			camera.timeHasBeenBlack = Mathf.MoveTowards(camera.timeHasBeenBlack, 0f, Time.unscaledDeltaTime * 1.5f);
		}

		if (WorldGeneration.unchipped)
		{
			fill = 0f;
		}

		var sleepColor = new Color(color.r, color.g, color.b, (camera.timeHasBeenBlack - 1f) * 0.3334f);
		if (camera.sleepFill != null) // Unity object — ==
		{
			camera.sleepFill.fillAmount = fill;
			camera.sleepFill.color = sleepColor;
		}

		if (camera.consciousnessECG != null) // Unity object — ==
		{
			var raw = camera.consciousnessECG.GetComponent<RawImage>();
			if (raw != null) // Unity object — ==
			{
				raw.color = sleepColor;
			}

			camera.consciousnessECG.active = sleepColor.a > 0f;
		}

		if (camera.consciousnessText != null) // Unity object — ==
		{
			camera.consciousnessText.color = sleepColor;
		}

		if (camera.consciousnessVignette != null) // Unity object — ==
		{
			var conscious = watched.conscious || cutscene;
			var target = (conscious && watched.totalHappiness > -90f) ? 0f : (camera.didDeathScreen ? 0.75f : 1f);
			var existing = camera.consciousnessVignette.color;
			camera.consciousnessVignette.color = new Color(1f, 1f, 1f, Mathf.MoveTowards(existing.a, target, Time.deltaTime * 0.4f));
		}

		if (camera.consciousnessFade != null) // Unity object — ==
		{
			camera.consciousnessFade.color = new Color(0f, 0f, 0f, camera.blackAmount);
		}
	}
}
