// recipe: harmony-unit
// args: none
// returns: ok, patched, owners (comma-joined patch-class names of the CUO adapter's installed patches), error, detail
//
// Reads the LIVE Harmony registry and lists which patch classes from the CUO adapter
// assembly are installed right now. Stage 3 uses it to prove a disabled capability's
// unit is ABSENT from the installed set, not merely reported off by the installer.
((System.Func<string>)(() =>
{
	System.Type harmonyType = null;
	foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
	{
		harmonyType = assembly.GetType("HarmonyLib.Harmony");
		if (harmonyType != null)
		{
			break;
		}
	}

	if (harmonyType == null)
	{
		return "{\"ok\":false,\"error\":\"no-harmony\"}";
	}

	var all = (System.Collections.IEnumerable)harmonyType
		.GetMethod("GetAllPatchedMethods", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
		.Invoke(null, null);
	var infoMethod = harmonyType.GetMethod("GetPatchInfo", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
	if (all == null || infoMethod == null)
	{
		return "{\"ok\":false,\"error\":\"no-registry\"}";
	}

	var owners = new System.Collections.Generic.SortedSet<string>(System.StringComparer.Ordinal);
	var patched = 0;
	foreach (var method in all)
	{
		patched++;
		var info = infoMethod.Invoke(null, new object[] { method });
		if (info == null)
		{
			continue;
		}

		foreach (var collectionName in new[] { "Prefixes", "Postfixes", "Transpilers", "Finalizers" })
		{
			var collection = info.GetType().GetProperty(collectionName)?.GetValue(info) as System.Collections.IEnumerable;
			if (collection == null)
			{
				continue;
			}

			foreach (var patch in collection)
			{
				var patchMethod = patch?.GetType().GetProperty("PatchMethod")?.GetValue(patch) as System.Reflection.MethodBase;
				var declaring = patchMethod?.DeclaringType;
				if (declaring != null && declaring.Assembly.GetName().Name == "CasualtiesUnknownOnline.GameAdapter")
				{
					owners.Add(declaring.Name);
				}
			}
		}
	}

	return "{\"ok\":true,\"patched\":" + patched + ",\"owners\":\"" + string.Join(",", owners) + "\"}";
}))()
