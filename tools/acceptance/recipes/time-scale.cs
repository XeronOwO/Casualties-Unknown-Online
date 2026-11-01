// recipe: time-scale
// args: value=n
// returns: ok, timeScale, fixedDeltaTime, error
//
// Sets the game's own `Time.timeScale`, the knob the game's speed-up uses: every `Time.deltaTime`-driven system (a minigame's
// physics steps, its per-frame accumulation) then runs faster, so a long acceptance fixture takes seconds instead of minutes.
// A FIXTURE control: it changes the whole client's clock, so a batch that sets it puts it back to 1 in the same batch.
((System.Func<string>)(() => {
	var value = (float){{n:value}};
	if (value <= 0f) { return "{\"ok\":false,\"error\":\"bad-scale\"}"; }
	UnityEngine.Time.timeScale = value;
	return "{\"ok\":true,\"timeScale\":" + UnityEngine.Time.timeScale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"fixedDeltaTime\":" + UnityEngine.Time.fixedDeltaTime.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()