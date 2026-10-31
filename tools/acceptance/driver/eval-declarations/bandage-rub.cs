// eval declaration: bandage-rub
// declares: AcceptanceBandageRub
// serves: remote-medical-treatment-operations, topical-limb-action-shape — the bandage minigame that a remote treatment starts and
//         whose completion is what applies the effect to the patient.
//
// Why a declaration rather than a recipe: `BandageMinigame.PhysicsUpdate` measures the hand's ANGLE between FRAMES the game itself
// runs (it advances `bandageAngle` and calls `DoBandageAction` when the step is at least 20 degrees), so the gesture has to be
// performed across successive frames. A recipe's statements all run in one frame, and waiting inside one stalls the clock that
// measures the gesture.
//
// What it does: it moves the minigame's own HAND — `MinigameBase.main.handPos`, the field the game reads as the pointer — around a
// circle one step per frame with the hand held down, which is the same input a player's rotating drag produces. `bandageAngle`
// advances because the game computes it from that hand position; `DoBandageAction` is called by the GAME, never from here. The
// minigame ends on its own. The verdicts are read from the operator's log and the patient's own limb, never from this file.
using System.Collections;

public static class AcceptanceBandageRub
{
	public static string Start(int steps)
	{
		var camera = PlayerCamera.main;
		if (camera == null) { return "no-camera"; }
		camera.StartCoroutine(Rub(steps));
		return "started";
	}

	public static IEnumerator Rub(int steps)
	{
		UnityEngine.Debug.Log("[BandageRub] coroutine started, steps=" + steps);
		var angle = 0f;
		var sawMinigame = 0;
		var drove = 0;
		for (var n = 0; n < steps; n++)
		{
			// The singleton appears when a minigame starts and is gone when it ends, so it is re-read EVERY frame: a rub that captured
			// it once at the start saw null, broke out silently, and looked exactly like a minigame that never came (measured
			// 2026-11-01: no counter line at all from a 120-frame rub).
			var main = MinigameBase.main;
			if (main == null) { yield return null; continue; }

			// The minigame appears a frame or two AFTER the release that starts it, and it ends on its own: wait for it, drive it while
			// it lasts, and spend the rest of the budget skipping frames rather than breaking on the first empty one.
			if (main.currentMinigame == null || !(main.currentMinigame is BandageMinigame)) { yield return null; continue; }

			sawMinigame++;
			main.handClicking = true;
			main.handStartedClicking = n == 0;
			main.handStoppedClicking = false;
			angle += 30f;
			if (angle >= 360f) { angle -= 360f; }
			// Radius above the game's own 250-unit floor, so its clamp branch does not fight the gesture.
			main.handPos = new UnityEngine.Vector2(UnityEngine.Mathf.Sin(angle * 0.017453292f), UnityEngine.Mathf.Cos(angle * 0.017453292f)) * 320f;
			drove++;
			yield return null;
		}
		UnityEngine.Debug.Log("[BandageRub] frames with a bandage minigame: " + sawMinigame + ", driven: " + drove);
		if (MinigameBase.main != null)
		{
			MinigameBase.main.handStoppedClicking = true;
			MinigameBase.main.handClicking = false;
		}
	}
}
