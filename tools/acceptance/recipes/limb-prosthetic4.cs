// recipe: limb-prosthetic4
// args: mode=s limb=n item=s
// returns: ok, result, error
//
// The prosthetic reading: `mode=set` puts the native ProstheticInfo on the local body's limb
// (the same call the item's useLimbAction makes), and `mode=read` lists every body in the scene
// with the limbs that carry a prosthetic — the clone of the owner is the one a teammate sees.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	if (mode == "read") {
		var bodies = UnityEngine.Object.FindObjectsOfType(typeof(Body), true);
		var lines = new System.Collections.Generic.List<string>();
		foreach (var entry in bodies) {
			var body = entry as Body;
			if (body == null || body.limbs == null) { continue; }
			var parts = new System.Collections.Generic.List<string>();
			for (var i = 0; i < body.limbs.Length; i++) {
				var limb = body.limbs[i];
				if (limb == null) { continue; }
				if (limb.prostheticInfo.hasProsthetic) { parts.Add(i + "=" + limb.prostheticInfo.itemId + "/" + limb.dismembered); }
			}
			lines.Add(body.name + "[" + body.limbs.Length + "]:" + (parts.Count == 0 ? "-" : string.Join(",", parts)));
		}
		return "{\"ok\":true,\"result\":\"" + string.Join(" | ", lines).Replace("\"", "'") + "\"}";
	}
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var local = camera.body;
	if (local == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	var index = {{n:limb}};
	if (index < 0 || index >= local.limbs.Length) { return "{\"ok\":false,\"error\":\"no-limb\"}"; }
	var target = local.limbs[index];
	if (target == null) { return "{\"ok\":false,\"error\":\"no-limb\"}"; }
	var itemId = {{s:item}};
	target.prostheticInfo = new Limb.ProstheticInfo { hasProsthetic = true, itemId = itemId, forceChange = 0f, armorChange = 0f, r = 1f, g = 1f, b = 1f };
	return "{\"ok\":true,\"result\":\"set:" + target.prostheticInfo.hasProsthetic + ":" + target.prostheticInfo.itemId + "\"}";
}))()
