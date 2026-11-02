// recipe: body-sleep
// args: none
// returns: ok, added, canTakeNap, called, error, detail
//
// Adds the `SleepingPills` component AND calls the game's own `Body.TakeANap` in the SAME eval. One call, because the host's
// authoritative vitals restore removes the component again: a separate add-then-nap pair loses the gate between the two calls
// (measured 2026-11-02: `body-pills` answered canTakeNap=true and the nap taken a moment later never started). The nap itself is
// `NapCoroutine` (Body.cs): it lays the body down and sets `sleeping = true` after about two seconds.
((System.Func<string>)(() => {
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var added = false;
	if (body.GetComponent<SleepingPills>() == null) { body.gameObject.AddComponent<SleepingPills>(); added = true; }
	var gate = body.canTakeNap;
	body.TakeANap();
	return "{\"ok\":true,\"added\":" + (added ? "true" : "false") + ",\"canTakeNap\":" + (gate ? "true" : "false") + ",\"called\":true}";
}))()