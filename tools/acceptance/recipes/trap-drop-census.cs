// recipe: trap-drop-census
// args: x=n y=n radius=n limit=n
// serves: member-report-bounds section C — the NetMsg.EntityEvent drop row's machine read: which destructive
//         trap/entity event this world really carries, where it stands, and the drop-table ROWS the host's
//         bound reads for that entity.
// returns: ok, x, y, radius, total, listed, entries (each kind, x, y, prefab, health, distance, effects,
//          anchorOwn, anchorPrefab, rowCount, rows: ids, min, max), error, detail
//
// Read-only. Lists the live BuildingEntity objects within <radius> of the absolute point (x,y) that carry one
// of the four DESTRUCTIVE entity-event carriers — a mine, a turret, a fragile crystal, an unstable crystal:
// the four kinds whose host application writes the trap entity's health and folds drops into the event — up
// to <limit> entries. `total` is THIS scene's live BuildingEntity count (the whole population, not the
// carriers), so a partial census is told apart from a small world.
//
// Each entry names the EntityEventKind a report for that entity would carry and, in `rows`, the table
// `GameBuildingDropTable.ForEntity` reads OFF THAT ENTITY: the PRODUCT's own reading, reached by reflection
// because it is internal, never a re-derivation in the probe. `anchorOwn` is a second product reading: the
// recipe calls `TrapEffectApplier.FindTrap<BuildingEntity>` at the entry's own position — the host's
// position-keyed lookup — and answers whether the entity it resolves is this entry's own building. Both must
// line up for the rows to be the table the host judges against. `rowCount` is the full row count; `rows` is
// capped at 12 entries so a prefab with many guaranteed rolls cannot truncate the whole answer (the
// evaluator's value ceiling, the truncation building-support-cells warns about).
//
// SCOPE OF THE TABLE, stated because it decides which target a run may forge against: `rows` is the ENTITY'S
// OWN prefab table only. For an EXPLODING carrier (`MineExploded`, `TurretSelfDestructed`,
// `CrystalUnstableExploded`) the host's bound is a SUPERSET — `TrapBuildingHealthScope` adds the tables of
// the entities the host's own blast damaged, read while those entities still exist — so a forged drop at an
// exploding carrier can be accepted by a table this census never listed. The FRAGILE crystal is the one
// target whose bound is exactly these rows (its host apply does not explode), which is why the forge's
// refusal row is driven there.
//
// The crystal kind is derived from the private `effects` list (`CrystalEffect` is a PLAIN class, not a
// component; `CrystalBehaviour` is the only component). NOTE: `TrapEntityScan.CrystalKinds` names the same
// intent but switches on the COMPONENT's runtime type name, so that scanner yields NO crystal rows — the
// trap layout never carries a crystal kind, filed as `docs/backlog/todo/trap-layout-crystal-rows-empty.md`;
// this recipe reads the effects instead of reproducing that gap.
//
// Run it on BOTH clients with the same point: the two lists agreeing is what makes the table the run forges
// against the host's own. The point is a WORLD position (`body-read` returns the local body's `positionX`/
// `positionY`; `item-drop` returns the x/y it created at) — the driver's `state` action carries no position.
// Read it AFTER the world has SETTLED: a member's scene materializes after `inWorld` flips, and a census
// taken in that window lists a partial population (batch 20261016-c: 289 entities with 0 requireGround at
// 7 s, then the host's own full 1904 at 25 s). One eval.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	const System.Reflection.BindingFlags statics = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var centerX = (float)({{n:x}});
	var centerY = (float)({{n:y}});
	var radius = (float)({{n:radius}});
	var limit = (int)({{n:limit}});
	if (radius <= 0f) { radius = 80f; }
	if (radius > 600f) { radius = 600f; }
	if (limit <= 0) { limit = 20; }
	if (limit > 50) { limit = 50; }
	// A hand-built JSON value is escaped like the in-process driver's own: an id or a type name carrying a
	// quote or a line break must not turn the answer into the driver's ConvertFrom-Json failure.
	var esc = new System.Func<string, string>((value) => {
		if (value == null) { return ""; }
		var builder = new System.Text.StringBuilder();
		for (var i = 0; i < value.Length; i++) {
			var c = value[i];
			if (c == '\\' || c == '"') { builder.Append('\\').Append(c); }
			else if (c == '\n') { builder.Append("\\n"); }
			else if (c == '\r') { builder.Append("\\r"); }
			else if (c == '\t') { builder.Append("\\t"); }
			else if (c < ' ') { builder.Append(' '); }
			else { builder.Append(c); }
		}
		return builder.ToString();
	});
	var tableType = System.Type.GetType("CasualtiesUnknownOnline.GameAdapter.World.GameBuildingDropTable, CasualtiesUnknownOnline.GameAdapter");
	var forEntity = tableType == null ? null : tableType.GetMethod("ForEntity", statics);
	if (forEntity == null) { return "{\"ok\":false,\"error\":\"no-drop-table-reader\",\"detail\":\"GameBuildingDropTable.ForEntity did not resolve; an empty rows list would be indistinguishable from a tableless prefab\"}"; }
	var applierType = System.Type.GetType("CasualtiesUnknownOnline.GameAdapter.World.TrapEffectApplier, CasualtiesUnknownOnline.GameAdapter");
	var findTrap = applierType == null ? null : applierType.GetMethod("FindTrap", statics);
	if (findTrap == null || !findTrap.IsGenericMethodDefinition) { return "{\"ok\":false,\"error\":\"no-anchor-reader\",\"detail\":\"TrapEffectApplier.FindTrap did not resolve; the entry's rows could not be tied to the entity the host resolves\"}"; }
	var findBuilding = findTrap.MakeGenericMethod(typeof(BuildingEntity));
	var all = UnityEngine.Object.FindObjectsOfType(typeof(BuildingEntity));
	if (all == null) { return "{\"ok\":false,\"error\":\"no-census\"}"; }
	var entries = new System.Text.StringBuilder();
	var listed = 0;
	for (var b = 0; b < all.Length; b++) {
		var building = all[b] as BuildingEntity;
		if (building == null) { continue; }
		var pos = building.transform.position;
		var dx = pos.x - centerX;
		var dy = pos.y - centerY;
		var distance = System.Math.Sqrt(dx * dx + dy * dy);
		if (distance > radius) { continue; }
		var kinds = new System.Collections.Generic.List<string>();
		var effects = new System.Collections.Generic.List<string>();
		if (building.GetComponent<MineScript>() != null) { kinds.Add("MineExploded"); }
		if (building.GetComponent<TurretScript>() != null) { kinds.Add("TurretSelfDestructed"); }
		var crystal = building.GetComponent<CrystalBehaviour>();
		if (crystal != null) {
			var effectsField = typeof(CrystalBehaviour).GetField("effects", instanceFlags);
			var rolled = effectsField == null ? null : effectsField.GetValue(crystal) as System.Collections.IEnumerable;
			if (rolled != null) {
				foreach (var effect in rolled) {
					if (effect == null) { continue; }
					var effectName = effect.GetType().Name;
					effects.Add(effectName);
					if (effectName == "CrystalFragile") { kinds.Add("CrystalFragileBroken"); }
					if (effectName == "CrystalUnstable") { kinds.Add("CrystalUnstableExploded"); }
				}
			}
		}
		if (kinds.Count == 0) { continue; }
		var rows = new System.Text.StringBuilder();
		var rowCount = 0;
		var bounds = forEntity.Invoke(null, new object[] { building }) as System.Collections.IEnumerable;
		if (bounds != null) {
			foreach (var bound in bounds) {
				if (bound == null) { continue; }
				if (rowCount < 12) {
					if (rowCount > 0) { rows.Append(','); }
					var boundType = bound.GetType();
					var idsProperty = boundType.GetProperty("ItemIds", instanceFlags);
					var ids = (idsProperty == null ? null : idsProperty.GetValue(bound, null)) as System.Collections.Generic.IReadOnlyList<string>;
					rows.Append("{\"ids\":[");
					if (ids != null) {
						for (var r = 0; r < ids.Count; r++) {
							if (r > 0) { rows.Append(','); }
							rows.Append('"').Append(esc(ids[r])).Append('"');
						}
					}
					rows.Append(']');
					var minProperty = boundType.GetProperty("MinCondition", instanceFlags);
					var maxProperty = boundType.GetProperty("MaxCondition", instanceFlags);
					var minValue = minProperty == null ? null : (float?)minProperty.GetValue(bound, null);
					var maxValue = maxProperty == null ? null : (float?)maxProperty.GetValue(bound, null);
					rows.Append(",\"min\":").Append(minValue.HasValue ? minValue.Value.ToString("R", inv) : "null");
					rows.Append(",\"max\":").Append(maxValue.HasValue ? maxValue.Value.ToString("R", inv) : "null");
					rows.Append('}');
				}
				rowCount = rowCount + 1;
			}
		}
		var resolved = findBuilding.Invoke(null, new object[] { new Vector2(pos.x, pos.y) }) as BuildingEntity;
		var anchorOwn = resolved == building;
		var anchorPrefab = resolved == null ? "" : resolved.id;
		for (var k = 0; k < kinds.Count && listed < limit; k++) {
			if (listed > 0) { entries.Append(','); }
			entries.Append("{\"kind\":\"").Append(esc(kinds[k])).Append('"');
			entries.Append(",\"x\":").Append(pos.x.ToString("R", inv));
			entries.Append(",\"y\":").Append(pos.y.ToString("R", inv));
			entries.Append(",\"distance\":").Append(distance.ToString("0.###", inv));
			entries.Append(",\"prefab\":\"").Append(esc(building.id)).Append('"');
			entries.Append(",\"health\":").Append(building.health.ToString("R", inv));
			entries.Append(",\"effects\":[");
			for (var e = 0; e < effects.Count; e++) {
				if (e > 0) { entries.Append(','); }
				entries.Append('"').Append(esc(effects[e])).Append('"');
			}
			entries.Append("],\"anchorOwn\":").Append(anchorOwn ? "true" : "false");
			entries.Append(",\"anchorPrefab\":\"").Append(esc(anchorPrefab)).Append('"');
			entries.Append(",\"rowCount\":").Append(rowCount.ToString(inv));
			entries.Append(",\"rows\":[").Append(rows.ToString()).Append("]}");
			listed = listed + 1;
		}
	}
	return "{\"ok\":true"
		+ ",\"x\":" + centerX.ToString("R", inv)
		+ ",\"y\":" + centerY.ToString("R", inv)
		+ ",\"radius\":" + radius.ToString("R", inv)
		+ ",\"total\":" + all.Length.ToString(inv)
		+ ",\"listed\":" + listed.ToString(inv)
		+ ",\"entries\":[" + entries.ToString() + "]}";
}))()
