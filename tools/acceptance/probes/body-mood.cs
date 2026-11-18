// recipe: body-mood
// args: value=n
// serves: operational recovery - a long batch that forces vitals and inflicts damage can leave the played body's happiness at
//         zero, which the game shows as depression (and its self-harm behaviour); this restores it after the batch
// returns: ok, before, after, error, detail
//
// Writes ONE field on the local body: `Body.happiness`. It exists because `/heal` restores health and vitals but does not move
// mood, so a character used for staging can come out of a batch depressed with every other value healthy.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var before = body.happiness;
	var value = (float)({{n:value}});
	body.happiness = value;
	return "{\"ok\":true,\"before\":" + before.ToString("0.###", inv)
		+ ",\"after\":" + body.happiness.ToString("0.###", inv) + "}";
}))()
