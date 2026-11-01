// recipe: limb-dismember
// args: limb=n
// returns: ok, limb, name, dismembered, error, detail
//
// Severs the LOCAL body's limb through the game's own `Limb.Dismember()`, the call the amputation minigame makes when its cut
// completes (`AmputationMinigame.cs:73-82`). It exists to measure the SOUND of that event without the minigame's fixture: the
// row's claim is about what every peer hears when a limb comes off, and that event is the same whichever path produced it.
((System.Func<string>)(() => {
	var index = (int){{n:limb}};
	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var limbs = camera.body.limbs;
	if (index < 0 || index >= limbs.Length || limbs[index] == null) { return "{\"ok\":false,\"error\":\"bad-limb\"}"; }
	var limb = limbs[index];
	limb.Dismember();
	return "{\"ok\":true,\"limb\":" + index + ",\"name\":\"" + limb.fullName + "\",\"dismembered\":" + (limb.dismembered ? "true" : "false") + "}";
}))()