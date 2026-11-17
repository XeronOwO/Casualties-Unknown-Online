// recipe: bandage-step
// args: degrees=n
// returns: ok, stage, angle, actions, minigame, error
//
// One FRAME of the bandage minigame's hand gesture, driven from a recipe instead of a declared coroutine
// (the in-client declaration channel is broken on this build: a sent type never appears in the evaluator,
// so `bandage-rub` cannot run). The case calls this once per driver round trip, which lands one game frame
// apart, and the game's own `BandageMinigame.PhysicsUpdate` measures the angle between those frames.
//
// It moves the minigame's own HAND (`MinigameBase.main.handPos`, the field the game reads as the pointer)
// six degrees ahead of the ring and holds the click, exactly the shape the `bandage-rub6` declaration
// measured: the hand must stay ON the ring (`Dot(angleVector, handPos.normalized) > 0.9`) or the game
// never advances its angle past the 20-degree action step. `DoBandageAction` is called by the GAME, never
// from here; the verdict is read from the patient's own limb afterwards.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var turn = (float)({{n:degrees}});
	var main = MinigameBase.main;
	if (main == null) { return "{\"ok\":false,\"stage\":\"no-minigame\"}"; }

	var minigame = main.currentMinigame;
	if (!(minigame is BandageMinigame bandage)) {
		return "{\"ok\":false,\"stage\":\"other\",\"minigame\":\"" + (minigame == null ? "none" : minigame.GetType().Name) + "\"}";
	}

	main.handClicking = true;
	main.handStartedClicking = true;
	main.handStoppedClicking = false;

	// The game measures the angle between the pointer's position on consecutive FRAMES, so the hand has to
	// move a little on every call: derive the current angle from the hand's own position, then lead it six
	// degrees. A hand that is set to the same place each call measures a zero delta and never completes an
	// action (measured 2026-11-18: 160 driven calls, `lastActionAngle` stayed 0).
	var current = main.handPos;
	var degrees = UnityEngine.Mathf.Atan2(current.x, current.y) * 57.29578f;
	var angle = degrees + turn;
	main.handPos = new UnityEngine.Vector2(UnityEngine.Mathf.Sin(angle * 0.017453292f), UnityEngine.Mathf.Cos(angle * 0.017453292f)) * 320f;

	return "{\"ok\":true,\"stage\":\"driving\",\"angle\":" + bandage.bandageAngle.ToString("F2", inv) + ",\"actions\":" + (bandage.lastActionAngle / 20f).ToString("F0", inv) + "}";
}))()
