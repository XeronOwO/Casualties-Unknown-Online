// recipe: harmony-info
// args: type=s method=s
// returns: ok, type, method, found, prefixOwners, prefixCount, postfixCount, transpilerCount, error, detail
//
// Asks Harmony what it actually applied to one game method. The project's rule is that a cause is established by reading, and "the patch is listed in the
// capability catalogue" is not the same fact as "Harmony patched the method this build has": a renamed or re-signatured member, a patch class the patcher
// never reached, or a method the game no longer declares all look identical from the outside (a captured event that never reports).
//
// `type` accepts the short game type name (`LockpingMinigame`, `PantSound`), resolved in the Assembly-CSharp assembly.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var typeName = {{s:type}};
	var methodName = {{s:method}};
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	System.Type target = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { target = assemblies[i].GetType(typeName, false); } catch { target = null; }
		if (target == null) {
			try { target = assemblies[i].GetType("" + typeName, false); } catch { target = null; }
		}
		if (target != null) { break; }
	}
	if (target == null) { return "{\"ok\":false,\"error\":\"no-type\",\"type\":\"" + typeName + "\"}"; }

	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
	var method = target.GetMethod(methodName, flags);
	if (method == null) {
		var all = target.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		var names = "";
		for (var i = 0; i < all.Length; i++) { if (all[i].Name == methodName) { method = all[i]; break; } if (names.Length < 200) { names += all[i].Name + ","; } }
		if (method == null) { return "{\"ok\":false,\"error\":\"no-method\",\"type\":\"" + typeName + "\",\"methods\":\"" + names + "\"}"; }
	}

	var info = HarmonyLib.Harmony.GetPatchInfo(method);
	if (info == null) { return "{\"ok\":true,\"type\":\"" + typeName + "\",\"method\":\"" + methodName + "\",\"found\":false,\"prefixOwners\":\"\",\"prefixCount\":0,\"postfixCount\":0,\"transpilerCount\":0}"; }
	var owners = "";
	foreach (var owner in info.Owners) { if (owners.Length > 0) { owners += "|"; } owners += owner; }

	return "{\"ok\":true,\"type\":\"" + typeName + "\",\"method\":\"" + methodName + "\",\"found\":true"
		+ ",\"prefixOwners\":\"" + owners + "\""
		+ ",\"prefixCount\":" + info.Prefixes.Count.ToString(inv)
		+ ",\"postfixCount\":" + info.Postfixes.Count.ToString(inv)
		+ ",\"transpilerCount\":" + info.Transpilers.Count.ToString(inv) + "}";
}))()
