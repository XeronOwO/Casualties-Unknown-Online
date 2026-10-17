// recipe: item-hold-unreported
// args: mode=s type=s id=s
// serves: validation-and-source-checks S3a (a member's use report for an item the host has never judged)
// returns: mode=hold → ok, mode, type, instanceId, slot, held, liquid, condition, depth;
//          mode=use → ok, mode, instanceId, type, used, liquidBefore, liquidAfter, condition, alive;
//          mode=read → ok, mode, instanceId, source(body|kernel), held/kind, liquid, condition;
//          on failure ok, error (plus detail where the code carries one)
//
// The forged-member half of the S3a acceptance row: this client ends up HOLDING an item whose creation it
// never reported, which is the state a modified client is in when it invents an item locally and reports a
// use of it instead of reporting the item.
//
//   hold — creates one usable item through the game's own grant (Utils.Create + Body.PickUpItem(force))
//          INSIDE an inventory-internal scope. The scope is CUO's own call identity
//          (GameAdapter CallContext's Origin.InternalReorder), which is what decides that a pickup is an
//          inventory reorder and not a world event — so the pickup report the game would have sent is
//          suppressed by the product's own classification, not by patching anything out. `type` is the
//          item definition id (the game's own Item.GlobalItems key), and the definition must be `usable`:
//          an item the game has no use action for reaches no use report at all.
//   use  — uses the item THIS run created, addressed by instance id and never by type: the body may carry
//          an honest item of the same type, and the honest one must not be the one used. The use is the
//          game's own `Body.UseItem`, the entry point the radial release and a recipe consumption take, so
//          CUO's use-report hook fires on it exactly as it does on a player's gesture.
//   read — one item's state by instance id, from the LOCAL body when it holds it and from the KERNEL record
//          otherwise (the host's own row — the same row replication, the save archive and a rejoin carry).
//          `source` says which answered, so "the host judged it" and "this body holds it" never collapse
//          into one reading. This is how the run shows both halves of the settlement: the refusal did not
//          destroy the member's copy, and the host's record carries the state the use produced.
//
// The item stays in the body: the run's next step is the carried-set statement that must land it on the
// host. No inner lambda captures a local: the evaluator's Mono REPL refuses that shape.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags statics = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var mode = {{s:mode}};
	var type = {{s:type}};
	var rawId = {{s:id}};
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	if (mode != "hold" && mode != "use" && mode != "read") {
		return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be hold, use or read\"}";
	}
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var findType = new System.Func<string, System.Type>((fullName) => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var index = 0; index < assemblies.Length; index++) {
			System.Type candidate = null;
			try { candidate = assemblies[index].GetType(fullName, false); } catch { candidate = null; }
			if (candidate != null) { return candidate; }
		}
		return null;
	});
	var instanceIdType = findType("CasualtiesUnknownOnline.GameAdapter.Items.ItemInstanceId");
	var idOf = new System.Func<Item, ulong>((item) => {
		if (item == null) { return 0UL; }
		var marker = item.GetComponent(instanceIdType);
		if (marker == null) { return 0UL; }
		var property = marker.GetType().GetProperty("Id", instanceFlags);
		if (property != null) { return System.Convert.ToUInt64(property.GetValue(marker, null)); }
		var field = marker.GetType().GetField("Id", instanceFlags);
		return field == null ? 0UL : System.Convert.ToUInt64(field.GetValue(marker));
	});
	var liquidOf = new System.Func<Item, string>((item) => {
		if (item == null) { return "none"; }
		var water = item.GetComponent<WaterContainerItem>();
		if (water == null) { return "none"; }
		var builder = new System.Text.StringBuilder();
		for (var i = 0; i < water.stack.Count; i++) {
			if (i > 0) { builder.Append(","); }
			builder.Append(water.stack[i].liquidId).Append("=")
				.Append(water.stack[i].amount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
		}
		return builder.ToString();
	});
	// Every item the body carries: slot occupants, worn items and their nested descendants.
	var carried = new System.Collections.Generic.List<Item>();
	for (var slot = 0; slot < body.slots.Length; slot++) {
		var occupant = body.GetItem(slot);
		if (occupant != null) { carried.Add(occupant); }
	}
	for (var limb = 0; limb < body.limbs.Length; limb++) {
		var limbTransform = body.limbs[limb].transform;
		for (var child = 0; child < limbTransform.childCount; child++) {
			var worn = limbTransform.GetChild(child).GetComponent<Item>();
			if (worn != null) { carried.Add(worn); }
		}
	}
	for (var i = 0; i < carried.Count; i++) {
		var descendants = carried[i].GetComponentsInChildren<Item>(true);
		for (var d = 0; d < descendants.Length; d++) {
			if (descendants[d] != carried[i] && !carried.Contains(descendants[d])) { carried.Add(descendants[d]); }
		}
	}
	if (mode == "hold") {
		var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl)) as CasualtiesUnknownOnline.Runtime.Session.ISessionControl;
		if (session == null) { return "{\"ok\":false,\"error\":\"no-session\"}"; }
		if (session.Role != CasualtiesUnknownOnline.Runtime.Session.SessionRole.Guest) {
			return "{\"ok\":false,\"error\":\"not-guest\",\"detail\":\"an unreported item is a MEMBER's silence; run this recipe on the member, not the host\"}";
		}
		ItemInfo definition;
		if (!Item.GlobalItems.TryGetValue(type, out definition) || definition == null) {
			return "{\"ok\":false,\"error\":\"no-definition\",\"detail\":\"Item.GlobalItems carries no definition for " + type + "\"}";
		}
		if (!definition.usable) {
			return "{\"ok\":false,\"error\":\"not-usable\",\"detail\":\"" + type + " has no use action, so no use report could follow\"}";
		}
		var targetSlot = -1;
		for (var slot = 0; slot < body.slots.Length; slot++) {
			if (body.slots[slot] != null && body.slots[slot].canPickUp && !body.HoldingItem(slot)) { targetSlot = slot; break; }
		}
		if (targetSlot < 0) { return "{\"ok\":false,\"error\":\"no-free-slot\",\"detail\":\"every body slot is occupied or unusable\"}"; }
		var contextType = System.Type.GetType("CasualtiesUnknownOnline.GameAdapter.CallContext, CasualtiesUnknownOnline.GameAdapter");
		if (contextType == null) { return "{\"ok\":false,\"error\":\"no-call-context\",\"detail\":\"the GameAdapter's CallContext is not loaded in this process\"}"; }
		var originType = contextType.GetNestedType("Origin", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		var enter = contextType.GetMethod("Enter", statics);
		if (originType == null || enter == null) {
			return "{\"ok\":false,\"error\":\"no-call-context\",\"detail\":\"CallContext no longer offers Enter(Origin)\"}";
		}
		var scope = enter.Invoke(null, new object[] { System.Enum.Parse(originType, "InternalReorder") }) as System.IDisposable;
		Item held = null;
		var failure = "";
		try {
			var spawned = Utils.Create(type, body.transform.position, 0f);
			if (spawned == null) { failure = "create-failed"; }
			else {
				held = spawned.GetComponent<Item>();
				if (held == null) { UnityEngine.Object.Destroy(spawned); failure = "create-failed"; }
				else {
					body.PickUpItem(held, targetSlot, true);
					if (!body.HoldingItem(held)) { failure = "pickup-refused"; }
				}
			}
		}
		catch (System.Exception e) { failure = e.GetType().Name + ": " + e.Message; }
		finally {
			if (scope != null) { scope.Dispose(); }
		}
		if (failure.Length != 0) {
			return "{\"ok\":false,\"error\":\"hold-failed\",\"detail\":\"" + failure.Replace("\\", "/").Replace("\"", "'") + "\"}";
		}
		var heldId = idOf(held);
		if (heldId == 0UL) {
			return "{\"ok\":false,\"error\":\"no-instance-id\",\"detail\":\"the created item carries no instance id, so no report could name it\"}";
		}
		var heldSlot = -1;
		for (var slot = 0; slot < body.slots.Length; slot++) {
			if (body.GetItem(slot) == held) { heldSlot = slot; break; }
		}
		var depthField = contextType.GetField("_depth", statics);
		return "{\"ok\":true,\"mode\":\"hold\",\"type\":\"" + type + "\""
			+ ",\"instanceId\":\"" + heldId.ToString(inv) + "\""
			+ ",\"slot\":" + heldSlot.ToString(inv)
			+ ",\"held\":" + (body.HoldingItem(held) ? "true" : "false")
			+ ",\"liquid\":\"" + liquidOf(held) + "\""
			+ ",\"condition\":" + held.condition.ToString("0.###", inv)
			+ ",\"depth\":" + (depthField == null ? -1 : System.Convert.ToInt32(depthField.GetValue(null))) + "}";
	}
	var wantedId = 0UL;
	if (!ulong.TryParse(rawId, System.Globalization.NumberStyles.None, inv, out wantedId) || wantedId == 0UL) {
		return "{\"ok\":false,\"error\":\"bad-id\",\"detail\":\"id must be a non-zero decimal item instance id\"}";
	}
	Item target = null;
	for (var i = 0; i < carried.Count; i++) {
		if (idOf(carried[i]) == wantedId) { target = carried[i]; break; }
	}
	if (target == null) {
		var facts = services.GetService(typeof(CasualtiesUnknownOnline.Application.Kernel.IKernelItemFacts)) as CasualtiesUnknownOnline.Application.Kernel.IKernelItemFacts;
		if (facts == null) { return "{\"ok\":false,\"error\":\"no-kernel-facts\",\"detail\":\"no item record is reachable in this process\"}"; }
		var row = facts.FindItem(wantedId);
		if (row == null) {
			return "{\"ok\":false,\"error\":\"unknown-item\",\"detail\":\"neither this body nor this client's record holds #" + wantedId.ToString(inv) + "\"}";
		}
		var located = row.Value;
		var liquids = new System.Text.StringBuilder();
		for (var i = 0; i < located.Data.Liquids.Count; i++) {
			if (i > 0) { liquids.Append(","); }
			liquids.Append(located.Data.Liquids[i].LiquidId).Append("=")
				.Append(located.Data.Liquids[i].Amount.ToString("0.###", inv));
		}
		return "{\"ok\":true,\"mode\":\"read\",\"source\":\"kernel\""
			+ ",\"instanceId\":\"" + wantedId.ToString(inv) + "\""
			+ ",\"type\":\"" + located.Identity.DefinitionId + "\""
			+ ",\"kind\":\"" + located.Location.Kind + "\""
			+ ",\"owner\":\"" + located.Location.Owner.Value.ToString(inv) + "\""
			+ ",\"revision\":" + located.Revision.ToString(inv)
			+ ",\"liquid\":\"" + liquids + "\""
			+ ",\"condition\":" + located.Data.Condition.ToString("0.###", inv) + "}";
	}
	if (mode == "read") {
		return "{\"ok\":true,\"mode\":\"read\",\"source\":\"body\""
			+ ",\"instanceId\":\"" + wantedId.ToString(inv) + "\""
			+ ",\"type\":\"" + target.id + "\""
			+ ",\"held\":" + (body.HoldingItem(target) ? "true" : "false")
			+ ",\"liquid\":\"" + liquidOf(target) + "\""
			+ ",\"condition\":" + target.condition.ToString("0.###", inv) + "}";
	}
	var definitionId = target.id;
	var beforeLiquid = liquidOf(target);
	var usable = target.Stats != null && target.Stats.usable;
	body.UseItem(target);
	var afterLiquid = "destroyed";
	var afterCondition = "-1";
	try { afterLiquid = liquidOf(target); afterCondition = target.condition.ToString("0.###", inv); }
	catch (System.Exception e) { afterLiquid = "destroyed:" + e.GetType().Name; }
	var stillThere = UnityEngine.Object.FindObjectsOfType(typeof(Item));
	var alive = false;
	for (var i = 0; i < stillThere.Length; i++) {
		var candidate = stillThere[i] as Item;
		if (candidate != null && candidate == target) { alive = true; break; }
	}
	return "{\"ok\":true,\"mode\":\"use\",\"instanceId\":\"" + wantedId.ToString(inv) + "\""
		+ ",\"type\":\"" + definitionId + "\""
		+ ",\"used\":" + (usable ? "true" : "false")
		+ ",\"liquidBefore\":\"" + beforeLiquid + "\""
		+ ",\"liquidAfter\":\"" + afterLiquid + "\""
		+ ",\"condition\":" + afterCondition
		+ ",\"alive\":" + (alive ? "true" : "false") + "}";
}))()
