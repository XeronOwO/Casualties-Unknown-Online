// recipe: world-object-census
// args: none
// serves: generation-identity-remaining-families rows 1, 4 and 5 (trap materialization / cross-peer agreement)
// returns: ok, utc, total, groups, error
//
// Counts every live BuildingEntity in THIS client's scene, grouped by the prefab id the entity was
// created from (`BuildingEntity.id`) — the prefab-keyed view a trap-layout verification needs: a stale
// snapshot must leave every count unchanged, an applied (pre-stamp) one must move the count of the
// forged entry's prefab. Read-only, one eval.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var all = UnityEngine.Object.FindObjectsOfType<BuildingEntity>(true);
	var counts = new System.Collections.Generic.Dictionary<string, int>();
	for (var i = 0; i < all.Length; i++) {
		var e = all[i];
		if (e == null) { continue; }
		var idValue = null as object;
		var idProperty = e.GetType().GetProperty("id", flags);
		if (idProperty != null) { idValue = idProperty.GetValue(e, null); }
		else {
			var idField = e.GetType().GetField("id", flags);
			if (idField != null) { idValue = idField.GetValue(e); }
		}
		var id = idValue == null ? "(null)" : System.Convert.ToString(idValue);
		if (id.Length == 0) { id = "(empty)"; }
		int count;
		counts[id] = counts.TryGetValue(id, out count) ? count + 1 : 1;
	}
	var ids = new System.Collections.Generic.List<string>(counts.Keys);
	ids.Sort(System.StringComparer.Ordinal);
	var sb = new System.Text.StringBuilder();
	for (var i = 0; i < ids.Count; i++) {
		if (i > 0) { sb.Append(','); }
		sb.Append("{\"id\":\"").Append(ids[i]).Append("\",\"count\":").Append(counts[ids[i]].ToString(inv)).Append('}');
	}
	return "{\"ok\":true,\"utc\":\"" + System.DateTime.UtcNow.ToString("o") + "\",\"total\":" + all.Length.ToString(inv)
		+ ",\"groups\":[" + sb.ToString() + "]}";
}))()
