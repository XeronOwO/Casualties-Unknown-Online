// recipe: harmony-unit
// args: none
// returns: ok, patched, owners (comma-joined patch-class names of the CUO adapter's installed patches), sources (comma-joined Harmony assembly names), error
//
// Reads the LIVE Harmony registry and lists which patch classes from the CUO adapter
// assembly are installed right now. Stage 3 uses it to prove a disabled capability's
// unit is ABSENT from the installed set, not merely reported off by the installer.
// More than one HarmonyLib copy can be loaded (the plugin host's and another plugin's);
// every copy's registry is asked and the answers are merged.
((System.Func<string>)(() =>
{
	var owners = new System.Collections.Generic.SortedSet<string>(System.StringComparer.Ordinal);
	var sources = new System.Collections.Generic.SortedSet<string>(System.StringComparer.Ordinal);
	var patched = 0;
	foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
	{
		var harmonyType = assembly.GetType("HarmonyLib.Harmony");
		if (harmonyType == null)
		{
			continue;
		}

		var all = harmonyType.GetMethod("GetAllPatchedMethods", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.Invoke(null, null) as System.Collections.IEnumerable;
		var infoMethod = harmonyType.GetMethod("GetPatchInfo", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
		if (all == null || infoMethod == null)
		{
			continue;
		}

		sources.Add(assembly.GetName().Name);
		var seen = new System.Collections.Generic.HashSet<System.Reflection.MethodBase>();
		foreach (var method in all)
		{
			if (!seen.Add(method as System.Reflection.MethodBase))
			{
				continue;
			}

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
	}

	if (sources.Count == 0)
	{
		return "{\"ok\":false,\"error\":\"no-harmony\"}";
	}

	return "{\"ok\":true,\"patched\":" + patched + ",\"owners\":\"" + string.Join(",", owners) + "\",\"sources\":\"" + string.Join(",", sources) + "\"}";
}))()
