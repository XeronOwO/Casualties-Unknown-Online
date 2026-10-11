// recipe: seed-splint
// args: limb=n
// serves: medical-state-names-no-kind
// returns: ok, limb, added, condition, conditionLossMinute, item, splinted, components, error, detail
//
// Places ONE splint on a limb of the LOCAL body. The Stage-3 removal family's acceptance needs a splint
// on a peer's limb before the removal can be driven, and nothing in the UI vocabulary grants one on
// demand — so this is a DECLARED setup substitution, and the removal it enables stays the native path
// under test.
//
// The write mirrors the shape the game's own splint application uses (`Item.cs`'s `splint` useLimbAction:
// the component, the item's condition, `conditionLossMinute = 0.015f`, `item = "splint"`), with the
// component added the way CUO's own limb-component codec adds one. `condition = 100f` matters: a splint
// at condition 0 makes the component's own `Update` call `TakeOff` on its first frame.
//
// It only PLACES a splint. Clearing one is refused by name rather than faked — the removal under test is
// the peer-facing operation, not a local component delete.
//
// The `splinted` field it reports is Unity's own timing, not a failed write: `SplintLimb.Start` is what sets
// `limb.splinted = true`, and `Start` has not run in the frame the component was added — so a SUCCESSFUL
// seed answers `added:true, splinted:false, components:"SplintLimb"`, and `components` is the recipe's own
// statement of what it added rather than a read-back.
((System.Func<string>)(() => {
	var limbIndex = (int){{n:limb}};
	var num = new System.Func<float, string>((value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	if (limbIndex < 0 || limbIndex >= body.limbs.Length) { return "{\"ok\":false,\"error\":\"limb-out-of-range\",\"detail\":\"limb " + limbIndex + " of " + body.limbs.Length + "\"}"; }
	var limb = body.limbs[limbIndex];
	if (limb == null) { return "{\"ok\":false,\"error\":\"no-such-limb\",\"detail\":\"limb " + limbIndex + "\"}"; }
	if (limb.dismembered) { return "{\"ok\":false,\"error\":\"limb-dismembered\",\"detail\":\"limb " + limbIndex + "\"}"; }
	if (limb.isHead || limb.isVital) { return "{\"ok\":false,\"error\":\"limb-ineligible\",\"detail\":\"the game refuses a splint on the head or a vital limb\"}"; }

	var splint = limb.GetComponent<SplintLimb>();
	var added = false;
	if (splint == null) {
		splint = limb.gameObject.AddComponent<SplintLimb>();
		splint.conditionLossMinute = 0.015f;
		splint.condition = 100f;
		splint.item = "splint";
		added = true;
	}

	return "{\"ok\":true,\"limb\":" + limbIndex + ",\"added\":" + (added ? "true" : "false")
		+ ",\"condition\":" + num(splint.condition) + ",\"conditionLossMinute\":" + num(splint.conditionLossMinute)
		+ ",\"item\":\"" + splint.item + "\""
		+ ",\"splinted\":" + (limb.splinted ? "true" : "false")
		+ ",\"components\":\"SplintLimb\"}";
}))()
