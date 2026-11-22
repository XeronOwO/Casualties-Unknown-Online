// recipe: body-effects
// args: none
// returns: ok, opiate, consciousness, bloodVolume, alive, error
//
// Reads the LOCAL body's own effect values: the `Painkillers` component's `opiateAmount`
// (what fentanyl's `onHealthUse` raises — Liquids.cs:392-399, `ml * 0.1 * 420`) plus the
// body's consciousness and blood volume for context. It exists so the injection row can
// judge the effect on the patient's own client, not on the host's copy of the display body.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-player-camera\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var painkillers = body.GetComponent<Painkillers>();
	var opiate = painkillers != null ? painkillers.opiateAmount : 0f;
	var sb = new System.Text.StringBuilder();
	sb.Append("{\"ok\":true");
	sb.Append(",\"opiate\":").Append(opiate.ToString("0.###", inv));
	sb.Append(",\"consciousness\":").Append(body.consciousness.ToString("0.###", inv));
	sb.Append(",\"bloodVolume\":").Append(body.bloodVolume.ToString("0.###", inv));
	sb.Append(",\"alive\":").Append(body.alive ? "true" : "false");
	// The movement gate reads the body's own `conscious` flag (`PlayerCamera.cs:1023`), which is not the same fact as
	// the `consciousness` number: a body can sit at 1.5 and still refuse to walk. A row that reads only the number
	// cannot tell "the key was never read" from "the body is out cold" (measured 2026-11-22).
	sb.Append(",\"conscious\":").Append(body.conscious ? "true" : "false");
	sb.Append('}');
	return sb.ToString();
}))()
