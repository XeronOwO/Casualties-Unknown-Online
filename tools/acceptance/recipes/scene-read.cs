// recipe: scene-read
// args: none
// serves: layer-descent-spawn-separation, layer-change-menu-round-trip — the native scene/world/loading state beside a screen row
// returns: ok, scene, timeScale, world, generating, doingRegen, instantiating, worldExists, loadingActive, loadingName, depth, traveled, body, x, y
//
// The native state a frame alone cannot separate: `scene` + `world` + `generating` + `loadingActive` tell
// "still in the world scene, generating" from the menu, and x/y are the LOCAL body's own position. Reads the
// game's own WorldGeneration/PlayerCamera objects and the active scene name; read-only, one eval.
((System.Func<string>)(() => {
	try {
		var inv = System.Globalization.CultureInfo.InvariantCulture;
		var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
		var t = typeof(WorldGeneration);
		var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
		var world = WorldGeneration.world;
		var sb = new System.Text.StringBuilder();
		sb.Append("{\"ok\":true,\"scene\":\"").Append(scene).Append('"');
		sb.Append(",\"timeScale\":").Append(UnityEngine.Time.timeScale.ToString("0.###", inv));
		sb.Append(",\"world\":").Append(world == null ? "false" : "true");
		var generating = false;
		var regen = false;
		var instantiating = false;
		var loading = false;
		var loadingName = "";
		var depth = -1;
		var traveled = -1;
		var exists = false;
		if (world != null) {
			generating = world.generatingWorld;
			exists = world.worldExists;
			var go = world.loadingObject;
			if (go != null) { loading = go.activeSelf; loadingName = go.name; }
			var fr = t.GetField("doingRegen", flags);
			if (fr != null) { regen = (bool)fr.GetValue(world); }
			var fi = t.GetField("instantiatingWorld", flags);
			if (fi != null) { instantiating = (bool)fi.GetValue(world); }
			var fd = t.GetField("biomeDepth", flags);
			if (fd != null) { depth = (int)fd.GetValue(world); }
			var ft = t.GetField("totalTraveled", flags);
			if (ft != null) { traveled = (int)ft.GetValue(world); }
		}
		sb.Append(",\"generating\":").Append(generating ? "true" : "false");
		sb.Append(",\"doingRegen\":").Append(regen ? "true" : "false");
		sb.Append(",\"instantiating\":").Append(instantiating ? "true" : "false");
		sb.Append(",\"worldExists\":").Append(exists ? "true" : "false");
		sb.Append(",\"loadingActive\":").Append(loading ? "true" : "false");
		sb.Append(",\"loadingName\":\"").Append(loadingName).Append('"');
		sb.Append(",\"depth\":").Append(depth.ToString(inv));
		sb.Append(",\"traveled\":").Append(traveled.ToString(inv));
		var camera = PlayerCamera.main;
		var body = camera != null ? camera.body : null;
		sb.Append(",\"body\":").Append(body == null ? "false" : "true");
		if (body != null) {
			var p = body.transform.position;
			sb.Append(",\"x\":").Append(p.x.ToString("0.##", inv));
			sb.Append(",\"y\":").Append(p.y.ToString("0.##", inv));
		}
		sb.Append('}');
		return sb.ToString();
	} catch (System.Exception ex) {
		return "{\"ok\":false,\"error\":\"" + ex.GetType().Name + "\",\"detail\":\"" + ex.Message.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
	}
}))()
