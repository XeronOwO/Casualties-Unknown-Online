// recipe: container-panel
// args: mode=s container=s guest=s
// serves: guest-container-contents-ghost-drops-on-host
// returns: ok, mode, containerType, owner, opened, windowChildCount, error, detail
//
// Opens the game's own container window so a per-window frame can be captured and
// read:
//  - mode=local (any client): the local body's carried container (the own view), via
//    PlayerCamera.OpenContainer — the game's own window entry the CUO patch hooks.
//  - mode=remote (host/third): a remote player's container as THIS client renders it.
//    The CUO remote-backpack focus opens first through the registered presentation
//    entry (the same call the Online UI's open-backpack action makes; it keeps its
//    line-of-sight gate), then the game's own PlayerCamera.OpenContainer shows the
//    proxy's children — the host-view half of the contents row.
//  - mode=close (any client): the game's own PlayerCamera.CloseContainer, the radial
//    state cleared and the remote focus released, so a panel never lingers over later
//    captures.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	const System.Reflection.BindingFlags staticFlags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var mode = {{s:mode}};
	var containerType = {{s:container}};
	var guestArg = {{s:guest}};
	if (mode != "local" && mode != "remote" && mode != "close") {
		return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be local, remote or close\"}";
	}
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.SessionService)) as CasualtiesUnknownOnline.Runtime.Session.SessionService;
	if (session == null) { return "{\"ok\":false,\"error\":\"no-session\"}"; }
	var findType = new System.Func<string, System.Type>((fullName) => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++) {
			System.Type candidate = null;
			try { candidate = assemblies[i].GetType(fullName, false); } catch { candidate = null; }
			if (candidate != null) { return candidate; }
		}
		return null;
	});
	var markerOf = new System.Func<Item, UnityEngine.Component>((item) => {
		var components = item.GetComponents<UnityEngine.Component>();
		for (var i = 0; i < components.Length; i++) {
			if (components[i] != null && components[i].GetType().Name == "RemoteInventoryItemId") { return components[i]; }
		}
		return null;
	});
	var markerField = new System.Func<UnityEngine.Component, string, ulong>((component, name) => {
		var field = component.GetType().GetField(name, instanceFlags);
		return field == null ? 0UL : System.Convert.ToUInt64(field.GetValue(component));
	});
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var guestId = 0UL;
	if (guestArg != "auto") {
		ulong.TryParse(guestArg, out guestId);
	}
	if (mode == "close") {
		camera.CloseContainer();
		camera.radialOpen = false;
		var viewType = findType("CasualtiesUnknownOnline.GameAdapter.RemoteBackpackView");
		if (viewType != null) {
			var close = viewType.GetMethod("Close", staticFlags);
			if (close != null) { close.Invoke(null, null); }
		}
		return "{\"ok\":true,\"mode\":\"close\",\"containerType\":\"" + containerType + "\",\"opened\":false}";
	}
	if (mode == "local") {
		var body = camera.body;
		if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
		var items = new System.Collections.Generic.List<Item>();
		for (var slot = 0; slot < body.slots.Length; slot++) {
			var held = body.GetItem(slot);
			if (held != null) { items.Add(held); }
		}
		for (var limb = 0; limb < body.limbs.Length; limb++) {
			var limbTransform = body.limbs[limb].transform;
			for (var child = 0; child < limbTransform.childCount; child++) {
				var worn = limbTransform.GetChild(child).GetComponent<Item>();
				if (worn != null) { items.Add(worn); }
			}
		}
		for (var i = 0; i < items.Count; i++) {
			var descendants = items[i].GetComponentsInChildren<Item>(true);
			for (var d = 0; d < descendants.Length; d++) {
				if (descendants[d] != items[i] && !items.Contains(descendants[d])) { items.Add(descendants[d]); }
			}
		}
		Container container = null;
		for (var i = 0; i < items.Count; i++) {
			if (items[i].id != containerType) { continue; }
			var candidate = items[i].GetComponent<Container>();
			if (candidate == null) { continue; }
			container = candidate;
			break;
		}
		if (container == null) {
			return "{\"ok\":false,\"error\":\"no-local-container\",\"detail\":\"no carried item of type " + containerType + " carries a Container component\"}";
		}
		camera.radialOpen = true;
		camera.OpenContainer(container);
		var contentsCount = 0;
		for (var child = 0; child < container.transform.childCount; child++) {
			if (container.transform.GetChild(child).GetComponent<Item>() != null) { contentsCount++; }
		}
		return "{\"ok\":true,\"mode\":\"local\",\"containerType\":\"" + containerType + "\",\"owner\":\""
			+ session.LocalSteamId + "\",\"opened\":" + (camera.currentContainer == container ? "true" : "false")
			+ ",\"contentsCount\":" + contentsCount.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
	}
	var presentation = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.GameAdapter.IRemoteInventoryPresentation)) as CasualtiesUnknownOnline.Runtime.GameAdapter.IRemoteInventoryPresentation;
	if (presentation == null) { return "{\"ok\":false,\"error\":\"no-remote-presentation\"}"; }
	if (guestId == 0UL) {
		var candidates = 0;
		foreach (var member in session.Members) {
			if (member.SteamId != session.LocalSteamId && member.InWorld) { candidates++; guestId = member.SteamId; }
		}
		if (candidates != 1) {
			return "{\"ok\":false,\"error\":\"ambiguous-guest\",\"detail\":\"pass the owner SteamId explicitly\"}";
		}
	}
	if (!presentation.OpenRemoteBackpack(guestId, "acceptance")) {
		return "{\"ok\":false,\"error\":\"open-refused\",\"detail\":\"the remote backpack entry refused (line of sight or no render clone)\"}";
	}
	Container proxy = null;
	var found = UnityEngine.Object.FindObjectsOfType(typeof(Item));
	for (var i = 0; i < found.Length; i++) {
		var item = found[i] as Item;
		if (item == null || item.id != containerType) { continue; }
		var marker = markerOf(item);
		if (marker == null || markerField(marker, "OwnerSteamId") != guestId) { continue; }
		var candidate = item.GetComponent<Container>();
		if (candidate == null) { continue; }
		proxy = candidate;
		break;
	}
	if (proxy == null) {
		return "{\"ok\":false,\"error\":\"no-proxy\",\"detail\":\"no rendered " + containerType + " proxy of " + guestId + " in this scene\"}";
	}
	camera.OpenContainer(proxy);
	var proxyContents = 0;
	for (var child = 0; child < proxy.transform.childCount; child++) {
		if (proxy.transform.GetChild(child).GetComponent<Item>() != null) { proxyContents++; }
	}
	return "{\"ok\":true,\"mode\":\"remote\",\"containerType\":\"" + containerType + "\",\"owner\":\"" + guestId
		+ "\",\"opened\":" + (camera.currentContainer == proxy ? "true" : "false")
		+ ",\"contentsCount\":" + proxyContents.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
