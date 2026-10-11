// recipe: seed-shrapnel
// args: limb=n count=n
// serves: cross-player-item-use-random-determinism
// returns: ok, limb, shrapnel, hasShrapnel, dismembered, skinHealth, bleedAmount, pain, error, detail
//
// Seeds LIVE shrapnel into one limb of the LOCAL body — the one world precondition the cross-player
// shrapnel rows need and the UI vocabulary cannot reach. Earlier batches recorded this as their biggest
// setup gap: up to five native pieces exist per limb, and nothing in the game's own surfaces grants them
// on demand.
//
// It seeds the LOCAL body on purpose. A limb's piece count travels the same way every other limb fact
// does: the local snapshot reads `Limb.shrapnel`, the character report carries it as
// `CharacterLimbMsg.Shrapnel`, and a peer's displayed limb is written from that report. Seeding HERE is
// therefore what makes the authoritative copy and every operator's displayed limb agree; writing the
// field on somebody else's clone would be overwritten by that client's next report.
//
// `limb` is REQUIRED and is never guessed. A run that seeded "some limb" would put the pieces wherever
// this recipe felt like it, which is exactly the fallback the entry rules forbid; an out-of-range limb and
// a dismembered one are refused by name instead. `count` is 0..5 — the game's own slot count
// (`ShrapnelMinigame` carries five) — and 0 clears the limb again.
//
// The write is the game's own public field (`Limb.shrapnel`); `Limb.hasShrapnel` is a DERIVED property
// (`shrapnel > 0`) and is therefore read back, never written. Nothing else re-seeds a seeded count: no
// game code assigns `shrapnel` outside the minigame that consumes it.
//
// The seeded count reaches the peers on the next character report, so a run waits one report interval
// before it starts a session on the limb.
((System.Func<string>)(() => {
	var num = new System.Func<float, string>((value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
	var limbIndex = (int){{n:limb}};
	var count = (int){{n:count}};
	if (count < 0 || count > 5) { return "{\"ok\":false,\"error\":\"count-out-of-range\",\"detail\":\"count must be 0..5, got " + count + "\"}"; }
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	if (limbIndex < 0 || limbIndex >= body.limbs.Length) { return "{\"ok\":false,\"error\":\"limb-out-of-range\",\"detail\":\"limb " + limbIndex + " of " + body.limbs.Length + "\"}"; }
	var limb = body.limbs[limbIndex];
	if (limb == null) { return "{\"ok\":false,\"error\":\"no-such-limb\",\"detail\":\"limb " + limbIndex + "\"}"; }
	if (limb.dismembered) { return "{\"ok\":false,\"error\":\"limb-dismembered\",\"detail\":\"limb " + limbIndex + "\"}"; }
	limb.shrapnel = count;
	return "{\"ok\":true,\"limb\":" + limbIndex
		+ ",\"shrapnel\":" + limb.shrapnel
		+ ",\"hasShrapnel\":" + (limb.hasShrapnel ? "true" : "false")
		+ ",\"dismembered\":" + (limb.dismembered ? "true" : "false")
		+ ",\"skinHealth\":" + num(limb.skinHealth)
		+ ",\"bleedAmount\":" + num(limb.bleedAmount)
		+ ",\"pain\":" + num(limb.pain) + "}";
}))()
