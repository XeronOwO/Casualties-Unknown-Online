// recipe: body-wet
// args: wetness=n temperature=n
// returns: ok, wetness, temperature, wasWater, error, detail
//
// Makes the LOCAL body wet enough for the game's own water-shake routine to fire, because that routine has a twelve-second timer nothing else can hurry:
// `Body.HandleDogWaterShaking` starts `WaterShake` (and its `dogshake` clip) only when `conscious && wetness > 30 && temperature < 36.6 && !bodyAffect.wasWater`
// has held for more than 12 s (reversing/Assembly-CSharp/Assembly-CSharp/Body.cs). The values are SEEDED, named as a substitution: this is how the sound
// row's fixture gets the game to run its own routine rather than a re-implementation of it.
//
// `wasWater` is cleared because a body that recently touched water skips the routine entirely, which is the branch the row is not about.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var wetness = (float)({{n:wetness}});
	var temperature = (float)({{n:temperature}});
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }

	body.wetness = wetness;
	body.temperature = temperature;

	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var affectField = body.GetType().GetField("bodyAffect", flags);
	var affect = affectField == null ? null : affectField.GetValue(body);
	var wasWaterField = affect == null ? null : affect.GetType().GetField("wasWater", flags);
	if (wasWaterField != null) { wasWaterField.SetValue(affect, false); }

	return "{\"ok\":true,\"wetness\":" + body.wetness.ToString("0.##", inv)
		+ ",\"temperature\":" + body.temperature.ToString("0.##", inv)
		+ ",\"wasWater\":" + (wasWaterField == null ? "unknown" : (wasWaterField.GetValue(affect) is bool value && value ? "true" : "false")) + "}";
}))()
