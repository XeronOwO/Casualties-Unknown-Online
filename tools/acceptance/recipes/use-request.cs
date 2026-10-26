// recipe: use-request
// args: target=s item=n dose=n
// serves: use-on-sleeping-target-refused (decision 270's cross-player rows)
// returns: ok, target, item, dose, error, detail
//
// Sends ONE cross-player item-use REQUEST through the product's own sender — `IPlayerInteractionControl.SendUseRequest(target,
// itemInstanceId, targetLimbIndex, doseMl)`, the call `PlayerInteractionService` exposes to the drag/release path — so a batch can exercise a
// use whose TARGET is another player. `target` is the receiver's SteamId, `item` the instance id of the item the REQUESTER carries (0 lets the
// sender auto-select, the production gesture's own "no specific item" case) and `dose` millilitres for the drinking family (0 for the others).
//
// Why reflection: `IPlayerInteractionControl` is a Runtime interface resolved through the bootstrap's service provider, and an in-process eval
// cannot compile against it. The type is looked up BY NAME, exactly like `give-probe` does for the bridge, and the delegate declares its own
// array because a nested lambda that captures a local fails with no diagnostic.
((System.Func<string>)(() => {
	var targetText = {{s:target}};
	var itemText = {{n:item}};
	var doseText = {{n:dose}};
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var findType = new System.Func<string, System.Type>((simpleName) => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++) {
			System.Type[] types;
			try { types = assemblies[i].GetTypes(); } catch { continue; }
			for (var t = 0; t < types.Length; t++) {
				if (types[t].Name == simpleName) { return types[t]; }
			}
		}
		return null;
	});
	var controlType = findType("IPlayerInteractionControl");
	if (controlType == null) { return "{\"ok\":false,\"error\":\"no-control-type\"}"; }
	var control = services.GetService(controlType);
	if (control == null) { return "{\"ok\":false,\"error\":\"control-not-registered\"}"; }
	var method = controlType.GetMethod("SendUseRequest");
	if (method == null) { return "{\"ok\":false,\"error\":\"no-send-use-request\"}"; }
	var target = System.Convert.ToUInt64(targetText);
	var item = System.Convert.ToUInt64(itemText);
	var dose = System.Convert.ToSingle(doseText, inv);
	try {
		method.Invoke(control, new object[] { target, item, -1, dose });
	} catch (System.Exception ex) {
		var inner = ex.InnerException != null ? ex.InnerException : ex;
		return "{\"ok\":false,\"error\":\"invoke-failed\",\"detail\":\"" + inner.Message.Replace("\"", "'") + "\"}";
	}
	return "{\"ok\":true,\"target\":" + target.ToString(inv)
		+ ",\"item\":" + item.ToString(inv)
		+ ",\"dose\":" + dose.ToString(inv) + "}";
}))()
