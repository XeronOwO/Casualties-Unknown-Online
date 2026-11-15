// recipe: layer-state
// args: none
// returns: ok, biomeDepth, halfHeight, panelActive, generating, regenerating, worldExists, error
//
// Reads the live layer state the descent decisions use. The drill pod's sink patches run inside
// WorldGeneration.RegenerateWorld; a blocked team gate must leave biomeDepth unchanged. During the
// regeneration the world object can be destroyed or half-torn-down, so this recipe answers `world-busy`
// instead of throwing: the caller polls until the new world is up.
((System.Func<string>)(() => {
	try {
		const System.Reflection.BindingFlags StaticAny = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
		const System.Reflection.BindingFlags InstanceAny = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
		System.Type genType = null;
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++) {
			try { if (genType == null) { genType = assemblies[i].GetType("WorldGeneration", false); } } catch { }
		}
		if (genType == null) { return "{\"ok\":false,\"error\":\"no-worldgeneration\"}"; }

		var worldField = genType.GetField("world", StaticAny);
		var world = worldField == null ? null : worldField.GetValue(null);
		if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
		if (world is UnityEngine.Object unityWorld && unityWorld == null) { return "{\"ok\":false,\"error\":\"world-busy\"}"; }

		var depthField = genType.GetField("biomeDepth", InstanceAny);
		var halfProperty = genType.GetProperty("halfHeight", InstanceAny);
		var panelField = genType.GetField("savePanel", InstanceAny);
		var generatingField = genType.GetField("generatingWorld", InstanceAny);
		var regeneratingField = genType.GetField("doingRegen", InstanceAny);
		var existsProperty = genType.GetProperty("worldExists", InstanceAny);

		var depth = depthField == null ? -1 : System.Convert.ToInt32(depthField.GetValue(world), System.Globalization.CultureInfo.InvariantCulture);
		var half = halfProperty == null ? -1 : System.Convert.ToInt32(halfProperty.GetValue(world, null), System.Globalization.CultureInfo.InvariantCulture);
		var panel = panelField == null ? null : panelField.GetValue(world);
		var panelActive = panel is UnityEngine.GameObject panelObject && panelObject != null && panelObject.activeSelf;
		var generating = generatingField != null && generatingField.GetValue(world) is bool flag && flag;
		var regenerating = regeneratingField != null && regeneratingField.GetValue(world) is bool regenFlag && regenFlag;
		var exists = existsProperty != null && existsProperty.GetValue(world, null) is bool alive && alive;

		return "{\"ok\":true,\"biomeDepth\":" + depth
			+ ",\"halfHeight\":" + half
			+ ",\"panelActive\":" + (panelActive ? "true" : "false")
			+ ",\"generating\":" + (generating ? "true" : "false")
			+ ",\"regenerating\":" + (regenerating ? "true" : "false")
			+ ",\"worldExists\":" + (exists ? "true" : "false") + "}";
	}
	catch {
		return "{\"ok\":false,\"error\":\"world-busy\"}";
	}
}))()
