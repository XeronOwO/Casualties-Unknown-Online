// eval declaration: bandage-rub6
// declares: AcceptanceBandageRub6
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

public static class AcceptanceBandageRub6
{
	/// <summary>
	/// The counters a recipe reads back. They live here rather than in a log line because a `Debug.Log` from this dynamic assembly
	/// reaches neither BepInEx's log nor Unity's `Player.log` (measured 2026-11-01), which left "the rub drove nothing" and "the rub
	/// never ran" indistinguishable. `bandage-rub-state` reads these fields.
	/// </summary>
	public static int Starts;
	public static int Saw;
	public static int Drove;
	public static bool Running;
	public static int MainNull;
	public static string Other = "";
	/// <summary>The game's own action counter: BandageMinigame.lastActionAngle grows by 20 per DoBandageAction, so its growth IS the number of actions the game performed.</summary>
	public static int Actions;

	public static string Start(int steps)
	{
		var camera = PlayerCamera.main;
		if (camera == null) { return "no-camera"; }
		Starts++;
		camera.StartCoroutine(Rub(steps));
		return "started";
	}

	public static IEnumerator Rub(int steps)
	{
		Running = true;
		var angle = 0f;
		var sawMinigame = 0;
		var drove = 0;
		for (var n = 0; n < steps; n++)
		{
			// The singleton appears when a minigame starts and is gone when it ends, so it is re-read EVERY frame: a rub that captured
			// it once at the start saw null, broke out silently, and looked exactly like a minigame that never came (measured
			// 2026-11-01: no counter line at all from a 120-frame rub).
			var main = MinigameBase.main;
			if (main == null) { MainNull++; yield return null; continue; }

			// The minigame appears a frame or two AFTER the release that starts it, and it ends on its own: wait for it, drive it while
			// it lasts, and spend the rest of the budget skipping frames rather than breaking on the first empty one.
			if (main.currentMinigame == null || !(main.currentMinigame is BandageMinigame)) { Other = main.currentMinigame == null ? "none" : main.currentMinigame.GetType().Name; yield return null; continue; }

			sawMinigame++;
			Saw = sawMinigame;
			main.handClicking = true;
			// `BandageMinigame.Update` computes holdingBandage ONLY on the frame handStartedClicking is true, so that edge belongs to the
			// first DRIVEN frame — an n==0 test keys it to the loop counter, which has already advanced past 0 by the time a minigame
			// appears (measured 2026-11-01: 34 driven frames, zero actions).
			main.handStartedClicking = drove == 0;
			main.handStoppedClicking = false;
			// The hand must stay ON the ring: BandageMinigame.holdingBandage is Dot(angleVector, handPos.normalized) > 0.9, and its
			// angle only advances while that holds — a hand that jumps 30 degrees per frame fails the test on the first one and the game
			// never performs an action (measured 2026-11-01: 35 driven frames, lastActionAngle 0). So the hand leads the minigame
			// angle by a few degrees, which keeps the dot above 0.9 and still advances the angle past its 20-degree action step.
			angle = (main.currentMinigame as BandageMinigame).bandageAngle + 6f;
			// Radius above the game's own 250-unit floor, so its clamp branch does not fight the gesture.
			main.handPos = new UnityEngine.Vector2(UnityEngine.Mathf.Sin(angle * 0.017453292f), UnityEngine.Mathf.Cos(angle * 0.017453292f)) * 320f;
			drove++;
			Drove = drove;
			Actions = (main.currentMinigame as BandageMinigame).lastActionAngle / 20;
			yield return null;
		}
		if (MinigameBase.main != null)
		{
			MinigameBase.main.handStoppedClicking = true;
			MinigameBase.main.handClicking = false;
		}
		Running = false;
	}
}
