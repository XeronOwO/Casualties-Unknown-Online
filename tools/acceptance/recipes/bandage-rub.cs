// recipe: bandage-rub
// args: steps=n
// serves: remote-medical-treatment-operations, topical-limb-action-shape — the completion half of a bandage started on the remote
//         chart: the minigame's own hand is rotated once per frame until the game finishes the treatment.
// returns: ok, started, steps, error, detail
//
// Starts the `bandage-rub` declaration's coroutine. The recipe returns at once; the rub happens while the game keeps running, so
// `BandageMinigame.PhysicsUpdate` measures the angle between frames the game itself advances. The effect on the patient is then
// read from the patient's own limb, never from here.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var steps = (int)({{n:steps}});
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type rub = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { rub = assemblies[i].GetType("AcceptanceBandageRub", false); } catch { rub = null; }
		if (rub != null) { break; }
	}
	if (rub == null) { return "{\"ok\":false,\"error\":\"not-declared\",\"detail\":\"declare bandage-rub first\"}"; }
	var method = rub.GetMethod("Start");
	if (method == null) { return "{\"ok\":false,\"error\":\"no-start\"}"; }
	var started = (string)method.Invoke(null, new object[] { steps });
	return "{\"ok\":true,\"started\":\"" + started + "\",\"steps\":" + steps.ToString(inv) + "}";
}))()
