// recipe: body-place
// args: x=n y=n
// serves: guest-hears-only-some-block-break-sounds, host-eating-sound-not-heard-on-guest,
//         host-metal-scrap-block-place-sound-not-synced-to-guest, sync-player-pain-vocalizations-and-bark
// returns: ok, x, y, error, detail
//
// Places the local body at (x, y) — a declared setup substitution, used only to bring the two clients
// into one spot for the rows whose physical audibility a person has to judge later; the game's own
// physics settles the body afterwards. The run reads both positions first (body-read) and records the
// substitution in the record's Limits.
((System.Func<string>)(() => {
	var x = (float)({{n:x}});
	var y = (float)({{n:y}});
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	body.transform.position = new Vector3(x, y, body.transform.position.z);
	return "{\"ok\":true,\"x\":" + body.transform.position.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"y\":" + body.transform.position.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
