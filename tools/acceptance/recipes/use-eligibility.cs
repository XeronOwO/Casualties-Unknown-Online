// recipe: use-eligibility
// args: type=s
// returns: ok, type, found, condition, isUseItem, family, error, detail
//
// Asks the ADAPTER's own classifier whether a carried item of <type> is a one-shot cross-player USE item - the exact predicate
// the drag cue and the release both call (`LocalUseItemEligibility.IsUseItem`), fed with the same semantic services the adapter
// registers. This is how an item family is chosen deliberately instead of by trial: the gesture's own answer, read directly.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var wanted = {{s:type}};
	var asms = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type eligType = null, limbType = null, consumeType = null, wearType = null, solidType = null;
	var ns = new string[] { "CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.", "CasualtiesUnknownOnline.Runtime.GameAdapter.", "CasualtiesUnknownOnline.GameAdapter." };
	for (var i = 0; i < asms.Length; i++) {
		if (eligType == null) { try { eligType = asms[i].GetType("CasualtiesUnknownOnline.GameAdapter.LocalUseItemEligibility", false); } catch { eligType = null; } }
		for (var n = 0; n < ns.Length; n++) {
			if (limbType == null) { try { limbType = asms[i].GetType(ns[n] + "ILimbUseSemantics", false); } catch { limbType = null; } }
			if (consumeType == null) { try { consumeType = asms[i].GetType(ns[n] + "IConsumeSemantics", false); } catch { consumeType = null; } }
			if (wearType == null) { try { wearType = asms[i].GetType(ns[n] + "IWearSemantics", false); } catch { wearType = null; } }
			if (solidType == null) { try { solidType = asms[i].GetType(ns[n] + "ISolidFoodSemantics", false); } catch { solidType = null; } }
		}
	}
	if (eligType == null || limbType == null || consumeType == null || wearType == null || solidType == null) {
		return "{\"ok\":false,\"error\":\"no-classifier\",\"detail\":\"a type was not found\"}";
	}
	var camera = PlayerCamera.main;
	if (camera == null || camera.body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	Item found = null;
	for (var i = 0; i < camera.body.slots.Length; i++) {
		var held = camera.body.GetItem(i);
		if (held == null) { continue; }
		if (held.id == wanted) { found = held; break; }
	}
	if (found == null) { return "{\"ok\":true,\"type\":\"" + wanted + "\",\"found\":false}"; }
	var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var method = eligType.GetMethod("IsUseItem", flags);
	if (method == null) { return "{\"ok\":false,\"error\":\"no-IsUseItem\"}"; }
	var ps = method.GetParameters();
	if (ps.Length != 5) { return "{\"ok\":false,\"error\":\"arity\",\"detail\":\"" + ps.Length + "\"}"; }
	var args = new object[] { found, services.GetService(limbType), services.GetService(consumeType), services.GetService(wearType), services.GetService(solidType) };
	var verdict = "err";
	try { verdict = method.Invoke(null, args).ToString(); } catch (System.Exception ex) { verdict = "err:" + ex.GetType().Name; }
	return "{\"ok\":true,\"type\":\"" + wanted + "\",\"found\":true"
		+ ",\"condition\":" + found.condition.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"isUseItem\":" + (verdict == "True" ? "true" : "false")
		+ ",\"verdict\":\"" + verdict + "\"}";
}))()
