// recipe: harmony-probe
// args: none
// serves: pinyin-search-standalone-mod (the machine diagnosis: which methods does Harmony name as patched in this client?)
// returns: ok, patchedCount, hasRefresh, hasSimpleName, refreshOwners, error
//
// Reads Harmony's own patched-method registry (Harmony.GetAllPatchedMethods) — a patch that failed to apply is
// invisible in the game's log, so the registry is the instrument. The owners string names the patch class that owns
// each recorded patch on PlayerCamera.RefreshRecipeList.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var findType = new System.Func<string, System.Type>((simpleName) => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++) {
			System.Type[] types;
			try { types = assemblies[i].GetTypes(); } catch { continue; }
			for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } }
		}
		return null;
	});
	var harmonyType = findType("Harmony");
	if (harmonyType == null) { return "{\"ok\":false,\"error\":\"no-harmony\"}"; }
	var all = harmonyType.GetMethod("GetAllPatchedMethods", F);
	if (all == null) { return "{\"ok\":false,\"error\":\"no-GetAllPatchedMethods\"}"; }
	var methods = all.Invoke(null, null) as System.Collections.IEnumerable;
	var count = 0;
	var hasRefresh = false;
	var hasSimpleName = false;
	var owners = "";
	if (methods != null) {
		foreach (var method in methods) {
			count++;
			var info = method as System.Reflection.MethodBase;
			if (info == null) { continue; }
			if (info.Name == "RefreshRecipeList" && info.DeclaringType != null && info.DeclaringType.Name == "PlayerCamera") {
				hasRefresh = true;
				var getPatchInfo = harmonyType.GetMethod("GetPatchInfo", F);
				if (getPatchInfo != null) {
					object patchInfo;
					try { patchInfo = getPatchInfo.Invoke(null, new object[] { info }); } catch { patchInfo = null; }
					if (patchInfo != null) {
						var fields = patchInfo.GetType().GetFields(F);
						for (var f = 0; f < fields.Length; f++) {
							var fieldValue = fields[f].GetValue(patchInfo);
							if (fieldValue is System.Collections.IEnumerable list) {
								foreach (var entry in list) {
									var owner = entry.GetType().GetProperty("owner", F);
									var ownerName = owner != null ? System.Convert.ToString(owner.GetValue(entry, null)) : "?";
									owners = owners + fields[f].Name + ":" + ownerName + ";";
								}
							}
						}
					}
				}
			}
			if (info.Name == "get_simpleName") { hasSimpleName = true; }
		}
	}
	return "{\"ok\":true,\"patchedCount\":" + count + ",\"hasRefresh\":" + (hasRefresh ? "true" : "false") + ",\"hasSimpleName\":" + (hasSimpleName ? "true" : "false") + ",\"refreshOwners\":\"" + owners.Replace("\"", "'") + "\"}";
}))()
