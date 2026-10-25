// recipe: use-eligibility
// args: type=s mode=s
// returns: ok, mode, entries, count, error, detail
//
// mode=one (default, with type=<id>) asks the ADAPTER's classifier about one carried item: `LocalUseItemEligibility.IsUseItem`,
// the exact predicate the drag cue and the release both call, fed with the same semantic services the adapter registers.
// mode=scan walks every loaded Item prefab and reports the ids the classifier accepts, with the family it assigns, so a batch
// picks the item for a cross-player use from the game's own data instead of guessing.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var wanted = {{s:type}};
	var mode = {{s:mode}};
	var asms = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type eligType = null, limbType = null, consumeType = null, wearType = null, solidType = null, familyType = null;
	var ns = new string[] { "CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.", "CasualtiesUnknownOnline.Runtime.GameAdapter.", "CasualtiesUnknownOnline.GameAdapter." };
	for (var i = 0; i < asms.Length; i++) {
		if (eligType == null) { try { eligType = asms[i].GetType("CasualtiesUnknownOnline.GameAdapter.LocalUseItemEligibility", false); } catch { eligType = null; } }
		if (familyType == null) { try { familyType = asms[i].GetType("CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.ItemUseFamilyClassifier", false); } catch { familyType = null; } }
		for (var n = 0; n < ns.Length; n++) {
			if (limbType == null) { try { limbType = asms[i].GetType(ns[n] + "ILimbUseSemantics", false); } catch { limbType = null; } }
			if (consumeType == null) { try { consumeType = asms[i].GetType(ns[n] + "IConsumeSemantics", false); } catch { consumeType = null; } }
			if (wearType == null) { try { wearType = asms[i].GetType(ns[n] + "IWearSemantics", false); } catch { wearType = null; } }
			if (solidType == null) { try { solidType = asms[i].GetType(ns[n] + "ISolidFoodSemantics", false); } catch { solidType = null; } }
		}
	}
	if (eligType == null || limbType == null || consumeType == null || wearType == null || solidType == null) {
		return "{\"ok\":false,\"error\":\"no-classifier\"}";
	}
	var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var useItem = eligType.GetMethod("IsUseItem", flags);
	if (useItem == null) { return "{\"ok\":false,\"error\":\"no-IsUseItem\"}"; }
	var limb = services.GetService(limbType);
	var consume = services.GetService(consumeType);
	var wear = services.GetService(wearType);
	var solid = services.GetService(solidType);
	var inv = System.Globalization.CultureInfo.InvariantCulture;

	if (mode == "scan") {
		var all = UnityEngine.Resources.FindObjectsOfTypeAll<Item>();
		var seen = new System.Collections.Generic.Dictionary<string, string>();
		var entries = "";
		var count = 0;
		for (var i = 0; i < all.Length; i++) {
			var item = all[i];
			if (item == null || string.IsNullOrEmpty(item.id)) { continue; }
			if (seen.ContainsKey(item.id)) { continue; }
			seen[item.id] = "1";
			string verdict;
			try { verdict = useItem.Invoke(null, new object[] { item, limb, consume, wear, solid }).ToString(); } catch { verdict = "err"; }
			if (verdict != "True") { continue; }
			count++;
			if (count > 60) { continue; }
			var family = "?";
			if (familyType != null) {
				var classify = familyType.GetMethod("Classify", flags);
				if (classify != null) {
					try {
						var entryEnum = System.Enum.Parse(classify.GetParameters()[0].ParameterType, "Inventory");
						family = classify.Invoke(null, new object[] { entryEnum, item.id, null, limb, consume, wear, solid }).ToString();
					} catch { family = "err"; }
				}
			}
			entries += (entries.Length == 0 ? "" : ",") + "{\"id\":\"" + item.id + "\",\"family\":\"" + family + "\"}";
		}
		return "{\"ok\":true,\"mode\":\"scan\",\"count\":" + count.ToString(inv) + ",\"entries\":[" + entries + "]}";
	}

	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	Item found = null;
	for (var i = 0; i < camera.body.slots.Length; i++) {
		var held = camera.body.GetItem(i);
		if (held == null) { continue; }
		if (held.id == wanted) { found = held; break; }
	}
	if (found == null) { return "{\"ok\":true,\"mode\":\"one\",\"type\":\"" + wanted + "\",\"found\":false}"; }
	var answer = "err";
	try { answer = useItem.Invoke(null, new object[] { found, limb, consume, wear, solid }).ToString(); } catch (System.Exception ex) { answer = "err:" + ex.GetType().Name; }
	return "{\"ok\":true,\"mode\":\"one\",\"type\":\"" + wanted + "\",\"found\":true"
		+ ",\"condition\":" + found.condition.ToString("0.###", inv)
		+ ",\"isUseItem\":" + (answer == "True" ? "true" : "false")
		+ ",\"verdict\":\"" + answer + "\"}";
}))()
