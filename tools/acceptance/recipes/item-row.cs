// recipe: item-row
// args: id=s
// serves: a-dropped-container-arrives-empty-on-the-other-side, give-from-the-ground-swallows-the-item (row readings)
// returns: ok, id, present, kind, owner, parent, slot, revision, error, detail
//
// Reads ONE item's authoritative kernel row as this client holds it — the fact every custody question turns on. `IItemControl.QueryJudgedItems`
// is the same enumeration the custody projection uses, so this answers exactly what the projection would compare against, without a second
// bookkeeping path. A row that is absent answers `present:false` (the item was never judged on this client, or its row is Terminal and gone).
//
// Why reflection: `IItemControl` is a Runtime interface resolved through the bootstrap's service provider, and an in-process eval cannot compile
// against it. The type is looked up BY NAME, exactly like `use-request` does.
((System.Func<string>)(() => {
	var idText = {{s:id}};
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
	var controlType = findType("IItemControl");
	if (controlType == null) { return "{\"ok\":false,\"error\":\"no-control-type\"}"; }
	var control = services.GetService(controlType);
	if (control == null) { return "{\"ok\":false,\"error\":\"control-not-registered\"}"; }
	var method = controlType.GetMethod("QueryJudgedItems");
	if (method == null) { return "{\"ok\":false,\"error\":\"no-query-judged-items\"}"; }
	var rows = method.Invoke(control, null);
	if (rows == null) { return "{\"ok\":false,\"error\":\"no-rows\"}"; }
	var wanted = System.Convert.ToUInt64(idText);
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	System.Collections.IDictionary map = rows as System.Collections.IDictionary;
	if (map == null) { return "{\"ok\":false,\"error\":\"rows-not-a-dictionary\",\"detail\":\"" + rows.GetType().Name + "\"}"; }
	if (!map.Contains(wanted)) { return "{\"ok\":true,\"id\":" + wanted.ToString(inv) + ",\"present\":false}"; }
	var row = map[wanted];
	var location = row.GetType().GetProperty("Location").GetValue(row, null);
	var kind = location.GetType().GetProperty("Kind").GetValue(location, null);
	var owner = location.GetType().GetProperty("Owner").GetValue(location, null);
	var parent = location.GetType().GetProperty("ParentItemId").GetValue(location, null);
	var ownerValue = owner == null ? 0UL : System.Convert.ToUInt64(owner.GetType().GetProperty("Value").GetValue(owner, null));
	var data = row.GetType().GetProperty("Data").GetValue(row, null);
	var slot = data == null ? -1 : System.Convert.ToInt32(data.GetType().GetProperty("SlotIndex").GetValue(data, null));
	var revision = System.Convert.ToUInt64(row.GetType().GetProperty("Revision").GetValue(row, null));
	return "{\"ok\":true,\"id\":" + wanted.ToString(inv) + ",\"present\":true"
		+ ",\"kind\":\"" + kind.ToString() + "\",\"owner\":" + ownerValue.ToString(inv)
		+ ",\"parent\":" + System.Convert.ToUInt64(parent).ToString(inv)
		+ ",\"slot\":" + slot.ToString(inv) + ",\"revision\":" + revision.ToString(inv) + "}";
}))()
