// recipe: body-pills
// args: none
// returns: ok, present, canTakeNap, error, detail
//
// Puts the `SleepingPills` component on the local body, the second branch of `Body.canTakeNap` (the first is energy < 35 with low
// pain and sickness, which the host's authoritative vitals restore too fast to hold). A fixture write: the component is the game's
// own, and `canTakeNap` then answers true, so the nap path can be walked at all.
((System.Func<string>)(() => {
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var pills = body.GetComponent<SleepingPills>();
	var added = false;
	if (pills == null) { pills = body.gameObject.AddComponent<SleepingPills>(); added = true; }
	return "{\"ok\":true,\"added\":" + (added ? "true" : "false") + ",\"canTakeNap\":" + (body.canTakeNap ? "true" : "false") + "}";
}))()