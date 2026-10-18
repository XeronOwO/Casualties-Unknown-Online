// recipe: limb-read
// args: source=s limb=n target=s
// serves: medical-state-names-no-kind, cross-player-item-use-random-determinism, heal-cheat-command-state-reset
// returns: ok, source, limb, skinHealth, bleedAmount, pain, muscleHealth, shrapnel, hasShrapnel, splinted,
//          dislocated, dismembered, isHead, isVital, distanceToHeart, components, error, detail
//
// Reads ONE limb through the surface a row names, so a run compares what each side actually holds
// instead of inferring it:
//   source=local    the CALLER's own body — the patient's own limb after a slip, or the operator's
//   source=saved    the AUTHORITY's stored character snapshot for `target`, read through the same store
//                   the character-data handler writes (a guest body is report-driven there, which is
//                   exactly what the slip's regression row measures one report interval later)
//   source=display  the remote medical view's displayed body — what an operator or a third-party
//                   viewer sees of that peer while the view is open
//
// The component list is the limb's dynamic [Saveable] set (SplintLimb / TourniquetScript / ChilledLimb):
// it is part of the character report, and it is the fact a removal has to clear on the target's own
// body. `shrapnel`/`hasShrapnel` read the game's own field and its derived property.
//
// The three topology fields are read too: `isHead`/`isVital` drive the game's own bone-break damage,
// healing rate and infection rules, and `distanceToHeart` orders the tourniquet's lower limbs. A body
// reset that captured a limb row without them would write false/false/0 onto the live limb, so a
// `/heal` row reads all three before and after.
((System.Func<string>)(() => {
	var source = {{s:source}};
	var limbIndex = (int){{n:limb}};
	var targetText = {{s:target}};
	var num = new System.Func<float, string>((value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
	var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

	var skin = 0f;
	var bleed = 0f;
	var pain = 0f;
	var muscle = 0f;
	var shrapnel = 0;
	var hasShrapnel = false;
	var splinted = false;
	var dislocated = false;
	var dismembered = false;
	var isHead = false;
	var isVital = false;
	var distanceToHeart = 0;
	var componentText = "";

	if (source == "local" || source == "display") {
		var body = (Body)null;
		if (source == "local") {
			body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
			if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
		} else {
			// The view type is looked up by NAME rather than by field, so a rename of the static cannot
			// make this read a different object silently.
			var viewType = (System.Type)null;
			var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
			for (var i = 0; i < assemblies.Length; i++) {
				try { viewType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteMedicalView", false); } catch { viewType = null; }
				if (viewType != null) { break; }
			}
			if (viewType == null) { return "{\"ok\":false,\"error\":\"no-medical-view-type\"}"; }
			var displayProp = viewType.GetProperty("DisplayBody", flags);
			body = displayProp != null ? displayProp.GetValue(null, null) as Body : null;
			if (body == null) { return "{\"ok\":false,\"error\":\"no-display-body\",\"detail\":\"the remote medical view is not open on this client\"}"; }
		}

		if (limbIndex < 0 || limbIndex >= body.limbs.Length) { return "{\"ok\":false,\"error\":\"limb-out-of-range\",\"detail\":\"limb " + limbIndex + " of " + body.limbs.Length + "\"}"; }
		var limb = body.limbs[limbIndex];
		if (limb == null) { return "{\"ok\":false,\"error\":\"no-such-limb\",\"detail\":\"limb " + limbIndex + "\"}"; }
		skin = limb.skinHealth;
		bleed = limb.bleedAmount;
		pain = limb.pain;
		muscle = limb.muscleHealth;
		shrapnel = limb.shrapnel;
		hasShrapnel = limb.hasShrapnel;
		splinted = limb.splinted;
		dislocated = limb.dislocated;
		dismembered = limb.dismembered;
		isHead = limb.isHead;
		isVital = limb.isVital;
		distanceToHeart = limb.distanceToHeart;
		if (limb.GetComponent<SplintLimb>() != null) { componentText = "SplintLimb"; }
		if (limb.GetComponent<TourniquetScript>() != null) { componentText = componentText.Length > 0 ? componentText + ",TourniquetScript" : "TourniquetScript"; }
		if (limb.GetComponent<ChilledLimb>() != null) { componentText = componentText.Length > 0 ? componentText + ",ChilledLimb" : "ChilledLimb"; }
	} else if (source == "saved") {
		var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
		if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
		var store = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.CharacterData.CharacterDataStore)) as CasualtiesUnknownOnline.Runtime.Session.CharacterData.CharacterDataStore;
		if (store == null) { return "{\"ok\":false,\"error\":\"no-character-store\"}"; }
		var data = store.GetSavedCharacter(System.Convert.ToUInt64(targetText));
		if (data == null) { return "{\"ok\":false,\"error\":\"no-saved-character\",\"detail\":\"steamId " + targetText + "\"}"; }
		CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterLimbMsg row = null;
		for (var i = 0; i < data.Limbs.Count; i++) {
			if (data.Limbs[i].Index == limbIndex) { row = data.Limbs[i]; break; }
		}
		if (row == null) { return "{\"ok\":false,\"error\":\"no-such-limb\",\"detail\":\"limb " + limbIndex + " of " + data.Limbs.Count + "\"}"; }
		skin = row.SkinHealth;
		bleed = row.BleedAmount;
		pain = row.Pain;
		muscle = row.MuscleHealth;
		shrapnel = row.Shrapnel;
		hasShrapnel = row.Shrapnel > 0;
		splinted = row.Splinted;
		dislocated = row.Dislocated;
		dismembered = row.Dismembered;
		isHead = row.IsHead;
		isVital = row.IsVital;
		distanceToHeart = row.DistanceToHeart;
		for (var i = 0; i < row.Components.Count; i++) {
			componentText = componentText.Length > 0 ? componentText + "," + row.Components[i].TypeName : row.Components[i].TypeName;
		}
	} else {
		return "{\"ok\":false,\"error\":\"bad-source\",\"detail\":\"" + source + "\"}";
	}

	return "{\"ok\":true,\"source\":\"" + source + "\",\"limb\":" + limbIndex
		+ ",\"skinHealth\":" + num(skin) + ",\"bleedAmount\":" + num(bleed) + ",\"pain\":" + num(pain)
		+ ",\"muscleHealth\":" + num(muscle) + ",\"shrapnel\":" + shrapnel
		+ ",\"hasShrapnel\":" + (hasShrapnel ? "true" : "false")
		+ ",\"splinted\":" + (splinted ? "true" : "false")
		+ ",\"dislocated\":" + (dislocated ? "true" : "false")
		+ ",\"dismembered\":" + (dismembered ? "true" : "false")
		+ ",\"isHead\":" + (isHead ? "true" : "false")
		+ ",\"isVital\":" + (isVital ? "true" : "false")
		+ ",\"distanceToHeart\":" + distanceToHeart
		+ ",\"components\":\"" + componentText + "\"}";
}))()
