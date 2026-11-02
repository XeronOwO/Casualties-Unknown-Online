// recipe: body-sleep-force
// args: hold=1
// returns: ok, sleeping, consciousness, error, detail
//
// Writes `Body.sleeping` directly (a fixture write, like limb-infect writes an infection). It exists because the nap ROUTE does not
// hold on a synced client: `TakeANap` runs (`NapCoroutine` sets consciousness=10 and sleeping=true after ~2 s) and the next
// authoritative apply takes the body back out of it - measured 2026-11-02 on a recruited client: consciousness read 45.8 -> 12.9 ->
// 20.5 -> 25.9 -> 31.3 over five seconds with `sleeping` never true. The panel rows need a target that STAYS asleep, so the state is
// written instead of driven, and the drift itself is recorded on the ticket.
((System.Func<string>)(() => {
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	body.sleeping = true;
	body.consciousness = 10f;
	return "{\"ok\":true,\"sleeping\":" + (body.sleeping ? "true" : "false") + ",\"consciousness\":10}";
}))()