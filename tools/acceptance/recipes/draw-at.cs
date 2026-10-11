// recipe: draw-at
// args: x=n y=n
// serves: mod-cross-player-solid-food-semantics row 5 (the "nothing visible at the parked spot" row):
//         whether a captured frame can carry that row at a world point at all
// returns: ok, x, y, hits, chunkRenderer, chunkLookup, renderers
//
// Reports every ENABLED renderer whose bounds contain one world point, with its sorting layer and order —
// the draw stack a visual row is judged against — plus the chunk renderer the game's own `Item.Update`
// asks for at that point (`chunkLookup` says which way that call answered: `renderer`, `returned-null`,
// `nonrenderer:<type>`, `no-method`, `no-world` or `threw:<type>`, so a `none` chunkRenderer is never
// ambiguous). The frame-level use: an object whose sprite sits at an ORDER BELOW the terrain tilemap's
// cannot appear in a frame at that point whatever its own renderer says, so a "nothing visible" row is
// judged on the machine read there instead of on a frame; the run records the measurement and the ticket
// carries the re-wording. Read-only, one eval.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var x = (float)({{n:x}});
	var y = (float)({{n:y}});
	var point = new Vector3(x, y, 0f);
	var renderers = UnityEngine.Object.FindObjectsOfType<UnityEngine.Renderer>();
	var sb = new System.Text.StringBuilder();
	var hits = 0;
	for (var i = 0; i < renderers.Length; i++) {
		var renderer = renderers[i];
		if (renderer == null || !renderer.enabled) { continue; }
		var bounds = renderer.bounds;
		if (point.x < bounds.min.x || point.x > bounds.max.x || point.y < bounds.min.y || point.y > bounds.max.y) { continue; }
		hits++;
		if (sb.Length > 0) { sb.Append(','); }
		sb.Append("{\"name\":\"").Append(renderer.gameObject.name.Replace("\"", "'")).Append('"')
			.Append(",\"type\":\"").Append(renderer.GetType().Name).Append('"')
			.Append(",\"layer\":\"").Append(renderer.sortingLayerName).Append('"')
			.Append(",\"order\":").Append(renderer.sortingOrder.ToString(inv))
			.Append(",\"minY\":").Append(bounds.min.y.ToString("0.###", inv))
			.Append(",\"maxY\":").Append(bounds.max.y.ToString("0.###", inv)).Append('}');
	}
	var chunkText = "\"none\"";
	var lookupText = "no-world";
	var world = WorldGeneration.world;
	if (world != null) {
		lookupText = "no-method";
		var type = world.GetType();
		var methods = type.GetMethods(flags);
		for (var m = 0; m < methods.Length; m++) {
			if (methods[m].Name != "GetClosestChunkRenderer") { continue; }
			var parameters = methods[m].GetParameters();
			var invoked = null as object;
			try {
				if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Vector2)) { invoked = methods[m].Invoke(world, new object[] { new Vector2(x, y) }); }
				else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Vector3)) { invoked = methods[m].Invoke(world, new object[] { point }); }
				else if (parameters.Length == 2) { invoked = methods[m].Invoke(world, new object[] { (int)x, (int)y }); }
				else { continue; }
			}
			catch (System.Exception e) { lookupText = "threw:" + e.GetType().Name; break; }
			var rendererResult = invoked as UnityEngine.Renderer;
			if (rendererResult != null) {
				chunkText = "{\"name\":\"" + rendererResult.gameObject.name.Replace("\"", "'") + "\",\"layer\":\"" + rendererResult.sortingLayerName
					+ "\",\"order\":" + rendererResult.sortingOrder.ToString(inv) + ",\"enabled\":" + (rendererResult.enabled ? "true" : "false") + "}";
				lookupText = "renderer";
			}
			else if (invoked != null) { lookupText = "nonrenderer:" + invoked.GetType().Name; }
			else { lookupText = "returned-null"; }
			break;
		}
	}
	return "{\"ok\":true,\"x\":" + x.ToString("0.###", inv) + ",\"y\":" + y.ToString("0.###", inv)
		+ ",\"hits\":" + hits.ToString(inv)
		+ ",\"chunkRenderer\":" + chunkText
		+ ",\"chunkLookup\":\"" + lookupText + "\""
		+ ",\"renderers\":[" + sb.ToString() + "]}";
}))()
