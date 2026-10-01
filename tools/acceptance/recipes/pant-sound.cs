// recipe: pant-sound
// args: kind=s
// serves: sync-player-pain-vocalizations-and-bark, unhooked-item-and-body-sound-families
// returns: ok, kind, applied, note, error, detail
//
// Forces one PantSound one-shot through the game's own path, so the character-sound capture sees the
// native call it wraps:
//   bark  — PantSound.Bark (the B-key bark); sets eatTime to zero first, which is its own gate
//   growl — a very unhappy body, then TryGrowl (its random gate fires at -100 total happiness)
//   pain  — averagePain 100 and painTime -1; the next PantSound.Update plays a pain clip
//   yawn  — energy 34 and yawnTime 61; the next PantSound.Update plays a yawn clip
// The pain and yawn rows need one more frame before the clip plays, so the run reads its record after
// a bounded wait rather than in the same step.
((System.Func<string>)(() => {
	var kind = {{s:kind}};
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var pant = body.GetComponent<PantSound>();
	if (pant == null) { return "{\"ok\":false,\"error\":\"no-pant-sound\"}"; }
	if (kind == "bark") {
		body.eatTime = 0f;
		pant.Bark();
		return "{\"ok\":true,\"kind\":\"bark\",\"applied\":true}";
	}
	if (kind == "growl") {
		body.happiness = -100f;
		pant.TryGrowl();
		return "{\"ok\":true,\"kind\":\"growl\",\"applied\":true}";
	}
	if (kind == "pain") {
		Body.censorPain = false;
		body.averagePain = 100f;
		pant.painTime = -1f;
		return "{\"ok\":true,\"kind\":\"pain\",\"applied\":true,\"note\":\"the next PantSound.Update plays it\"}";
	}
	if (kind == "yawn") {
		body.energy = 34f;
		pant.yawnTime = 61f;
		return "{\"ok\":true,\"kind\":\"yawn\",\"applied\":true,\"note\":\"the next PantSound.Update plays it\"}";
	}
	return "{\"ok\":false,\"error\":\"bad-kind\",\"detail\":\"kind must be bark, growl, pain or yawn\"}";
}))()
