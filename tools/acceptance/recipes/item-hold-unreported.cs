// recipe: item-hold-unreported
// args: mode=s type=s id=s
// serves: validation-and-source-checks S3a (a member's use report for an item the host has never judged)
// returns: mode=hold → ok, mode, type, instanceId, slot, held, liquid, condition, depth;
//          mode=use → ok, mode, instanceId, type, used, liquidBefore, liquidAfter, condition, alive;
//          mode=read → ok, mode, instanceId, source(body|kernel), type, kind, owner, liquid, condition;
//          on failure ok, error (plus detail where the code carries one)
//
// The forged-member half of the S3a acceptance row: this client ends up HOLDING an item that carries an
// instance id the host has never judged — the state a modified client is in when it invents an item
// locally and reports a use of it instead of reporting the item.
//
//   hold — creates one usable item through the game's own grant (Utils.Create + Body.PickUpItem(force))
//          INSIDE an inventory-internal scope, then stamps its instance id with the PRODUCT'S OWN allocator
//          (ItemIdAllocator.EnsureId). Two product rules do the work, no patch and no hand-picked id:
//            · the scope is CUO's own call identity (GameAdapter CallContext's Origin.InternalReorder), which
//              is what decides that a pickup is an inventory reorder and not a world event — the pickup
//              report the game would have sent is suppressed by that classification;
//            · Item.Start allocates an id only for a STANDALONE WORLD item, so an item that is already in a
//              body when Start runs is never reported as a spawn. The id the allocator stamps here is
//              therefore a fact this client holds and the host does not: the carried-set statement is the
//              only channel that will ever tell the host about it.
//          `type` is the item definition id (the game's own Item.GlobalItems key) and must be `usable`: an
//          item the game has no use action for reaches no use report at all.
//   use  — uses the item this run created, addressed by instance id. The use is the game's own
//          `Body.UseItem`, the entry point the radial release and a recipe consumption take, so CUO's
//          use-report hook fires on it exactly as it does on a player's gesture — with the id the hold step
//          stamped, which is the whole point.
//   read — one item's state by instance id, from the LOCAL body when it holds it and from the KERNEL record
//          otherwise (the host's own row — the row replication, the save archive and a rejoin all carry).
//          `source` says which answered, so "the host judged it" and "this body holds it" never collapse
//          into one reading. This is how the run reads both halves of the settlement: the refusal did not
//          destroy the member's copy, and the host's record carries the state the use produced.
//
// The item stays in the body: the run's next step is the carried-set statement that must land it on the
// host. Two evaluator constraints shape this file, and both are the REPL's, not the product's: no inner
// lambda captures an enclosing local (the compiler refuses that shape — everything is inlined instead of
// written as a helper), and the internal scope is released BEFORE any later statement, so a throw from the
// game's own creation call cannot leave the scope open (the adapter's frame boundary is the backstop).
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
	var markerType = System.Type.GetType("CasualtiesUnknownOnline.GameAdapter.Items.ItemInstanceId, CasualtiesUnknownOnline.GameAdapter");
	if (markerType == null) { return "{\"ok\":false,\"error\":\"no-instance-marker\",\"detail\":\"the GameAdapter's ItemInstanceId is not loaded in this process\"}"; }
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
		if (!Item.GlobalItems.ContainsKey(type)) { return "{\"ok\":false,\"error\":\"no-definition\",\"detail\":\"Item.GlobalItems carries no definition for " + type + "\"}"; }
		var definition = Item.GlobalItems[type];
		if (definition == null) { return "{\"ok\":false,\"error\":\"no-definition\",\"detail\":\"Item.GlobalItems carries no definition for " + type + "\"}"; }
		if (!definition.usable) { return "{\"ok\":false,\"error\":\"not-usable\",\"detail\":\"" + type + " has no use action, so no use report could follow\"}"; }
		var targetSlot = -1;
		for (var slot = 0; slot < body.slots.Length; slot++) {
			if (body.slots[slot] != null && body.slots[slot].canPickUp && !body.HoldingItem(slot)) { targetSlot = slot; break; }
		}
		if (targetSlot < 0) { return "{\"ok\":false,\"error\":\"no-free-slot\",\"detail\":\"every body slot is occupied or unusable\"}"; }
		var contextType = System.Type.GetType("CasualtiesUnknownOnline.GameAdapter.CallContext, CasualtiesUnknownOnline.GameAdapter");
		if (contextType == null) { return "{\"ok\":false,\"error\":\"no-call-context\",\"detail\":\"the GameAdapter's CallContext is not loaded in this process\"}"; }
		var originType = contextType.GetNestedType("Origin", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		var enter = contextType.GetMethod("Enter", statics);
		if (originType == null || enter == null) { return "{\"ok\":false,\"error\":\"no-call-context\",\"detail\":\"CallContext no longer offers Enter(Origin)\"}"; }
		var scope = enter.Invoke(null, new object[] { System.Enum.Parse(originType, "InternalReorder") }) as System.IDisposable;
		var spawned = Utils.Create(type, body.transform.position, 0f);
		Item held = null;
		if (spawned != null) {
			held = spawned.GetComponent<Item>();
			if (held == null) { UnityEngine.Object.Destroy(spawned); }
			else {
				body.PickUpItem(held, targetSlot, true);
				if (!body.HoldingItem(held)) { held = null; }
			}
		}
		if (scope != null) { scope.Dispose(); }
		if (held == null) { return "{\"ok\":false,\"error\":\"hold-failed\",\"detail\":\"the game's own creation or pickup did not leave the item in the body\"}"; }
		// The product's own allocator: PatchBridge.Impl -> GameAdapterBridge's domains -> the item id
		// allocator. The domain field is the bridge's primary-constructor capture, so it is matched by
		// its TYPE instead of a name the compiler chose.
		var patchBridgeType = System.Type.GetType("CasualtiesUnknownOnline.GameAdapter.PatchBridge, CasualtiesUnknownOnline.GameAdapter");
		if (patchBridgeType == null) { return "{\"ok\":false,\"error\":\"no-item-allocator\",\"detail\":\"the GameAdapter's PatchBridge is not loaded in this process\"}"; }
		var implProperty = patchBridgeType.GetProperty("Impl", statics);
		if (implProperty == null) { return "{\"ok\":false,\"error\":\"no-item-allocator\",\"detail\":\"PatchBridge no longer exposes Impl\"}"; }
		var bridge = implProperty.GetValue(null, null);
		if (bridge == null) { return "{\"ok\":false,\"error\":\"no-item-allocator\",\"detail\":\"no bridge is bound in this process\"}"; }
		var bridgeFields = bridge.GetType().GetFields(instanceFlags);
		var domains = (object)null;
		for (var f = 0; f < bridgeFields.Length; f++) {
			if (bridgeFields[f].FieldType.Name == "GameAdapterDomains") { domains = bridgeFields[f].GetValue(bridge); break; }
		}
		if (domains == null) { return "{\"ok\":false,\"error\":\"no-item-allocator\",\"detail\":\"the bridge carries no GameAdapterDomains field\"}"; }
		var itemIdsField = domains.GetType().GetField("ItemIds", instanceFlags);
		if (itemIdsField == null) { return "{\"ok\":false,\"error\":\"no-item-allocator\",\"detail\":\"GameAdapterDomains carries no ItemIds field\"}"; }
		var allocator = itemIdsField.GetValue(domains);
		if (allocator == null) { return "{\"ok\":false,\"error\":\"no-item-allocator\",\"detail\":\"the item id allocator is null\"}"; }
		var ensureId = allocator.GetType().GetMethod("EnsureId", instanceFlags);
		if (ensureId == null) { return "{\"ok\":false,\"error\":\"no-item-allocator\",\"detail\":\"ItemIdAllocator no longer offers EnsureId(Item)\"}"; }
		var stampedId = System.Convert.ToUInt64(ensureId.Invoke(allocator, new object[] { held }));
		if (stampedId == 0UL) { return "{\"ok\":false,\"error\":\"no-instance-id\",\"detail\":\"the product's allocator refused to stamp an id on the created item\"}"; }
		var heldWater = held.GetComponent<WaterContainerItem>();
		var heldLiquid = "none";
		if (heldWater != null) {
			var heldBuilder = new System.Text.StringBuilder();
			for (var k = 0; k < heldWater.stack.Count; k++) {
				if (k > 0) { heldBuilder.Append(","); }
				heldBuilder.Append(heldWater.stack[k].liquidId).Append("=").Append(heldWater.stack[k].amount.ToString("0.###", inv));
			}
			heldLiquid = heldBuilder.ToString();
		}
		var heldSlot = -1;
		for (var slot = 0; slot < body.slots.Length; slot++) {
			if (body.GetItem(slot) == held) { heldSlot = slot; break; }
		}
		var depthField = contextType.GetField("_depth", statics);
		return "{\"ok\":true,\"mode\":\"hold\",\"type\":\"" + type + "\""
			+ ",\"instanceId\":\"" + stampedId.ToString(inv) + "\""
			+ ",\"slot\":" + heldSlot.ToString(inv)
			+ ",\"held\":true"
			+ ",\"liquid\":\"" + heldLiquid + "\""
			+ ",\"condition\":" + held.condition.ToString("0.###", inv)
			+ ",\"depth\":" + (depthField == null ? -1 : System.Convert.ToInt32(depthField.GetValue(null))) + "}";
	}
	var wantedId = 0UL;
	if (!ulong.TryParse(rawId, System.Globalization.NumberStyles.None, inv, out wantedId) || wantedId == 0UL) {
		return "{\"ok\":false,\"error\":\"bad-id\",\"detail\":\"id must be a non-zero decimal item instance id\"}";
	}
	Item target = null;
	for (var i = 0; i < carried.Count; i++) {
		var candidateMarker = carried[i].GetComponent(markerType);
		if (candidateMarker == null) { continue; }
		var candidateProperty = candidateMarker.GetType().GetProperty("Id", instanceFlags);
		if (candidateProperty == null) { continue; }
		if (System.Convert.ToUInt64(candidateProperty.GetValue(candidateMarker, null)) == wantedId) { target = carried[i]; break; }
	}
	if (target == null) {
		var facts = services.GetService(typeof(CasualtiesUnknownOnline.Application.Kernel.IKernelItemFacts)) as CasualtiesUnknownOnline.Application.Kernel.IKernelItemFacts;
		if (facts == null) { return "{\"ok\":false,\"error\":\"no-kernel-facts\",\"detail\":\"no item record is reachable in this process\"}"; }
		var row = facts.FindItem(wantedId);
		if (row == null) {
			return "{\"ok\":false,\"error\":\"unknown-item\",\"detail\":\"neither this body nor this client's record holds #" + wantedId.ToString(inv) + "\"}";
		}
		var located = row.Value;
		var kernelLiquid = "";
		for (var i = 0; i < located.Data.Liquids.Count; i++) {
			if (i > 0) { kernelLiquid = kernelLiquid + ","; }
			kernelLiquid = kernelLiquid + located.Data.Liquids[i].LiquidId + "=" + located.Data.Liquids[i].Amount.ToString("0.###", inv);
		}
		return "{\"ok\":true,\"mode\":\"read\",\"source\":\"kernel\""
			+ ",\"instanceId\":\"" + wantedId.ToString(inv) + "\""
			+ ",\"type\":\"" + located.Identity.DefinitionId + "\""
			+ ",\"kind\":\"" + located.Location.Kind + "\""
			+ ",\"owner\":\"" + located.Location.Owner.Value.ToString(inv) + "\""
			+ ",\"revision\":" + located.Revision.ToString(inv)
			+ ",\"liquid\":\"" + kernelLiquid + "\""
			+ ",\"condition\":" + located.Data.Condition.ToString("0.###", inv) + "}";
	}
	var targetWater = target.GetComponent<WaterContainerItem>();
	var targetLiquid = "none";
	if (targetWater != null) {
		var targetBuilder = new System.Text.StringBuilder();
		for (var k = 0; k < targetWater.stack.Count; k++) {
			if (k > 0) { targetBuilder.Append(","); }
			targetBuilder.Append(targetWater.stack[k].liquidId).Append("=").Append(targetWater.stack[k].amount.ToString("0.###", inv));
		}
		targetLiquid = targetBuilder.ToString();
	}
	if (mode == "read") {
		return "{\"ok\":true,\"mode\":\"read\",\"source\":\"body\""
			+ ",\"instanceId\":\"" + wantedId.ToString(inv) + "\""
			+ ",\"type\":\"" + target.id + "\""
			+ ",\"where\":\"body\""
			+ ",\"held\":" + (body.HoldingItem(target) ? "true" : "false")
			+ ",\"liquid\":\"" + targetLiquid + "\""
			+ ",\"condition\":" + target.condition.ToString("0.###", inv) + "}";
	}
	var definitionId = target.id;
	var usable = target.Stats != null && target.Stats.usable;
	body.UseItem(target);
	var afterWater = target.GetComponent<WaterContainerItem>();
	var afterLiquid = "destroyed";
	if (afterWater != null) {
		var afterBuilder = new System.Text.StringBuilder();
		for (var k = 0; k < afterWater.stack.Count; k++) {
			if (k > 0) { afterBuilder.Append(","); }
			afterBuilder.Append(afterWater.stack[k].liquidId).Append("=").Append(afterWater.stack[k].amount.ToString("0.###", inv));
		}
		afterLiquid = afterBuilder.ToString();
	}
	var afterCondition = "-1";
	var inScene = false;
	var allItems = UnityEngine.Object.FindObjectsOfType(typeof(Item));
	for (var i = 0; i < allItems.Length; i++) {
		var candidate = allItems[i] as Item;
		if (candidate != null && candidate == target) { inScene = true; break; }
	}
	if (inScene) { afterCondition = target.condition.ToString("0.###", inv); }
	return "{\"ok\":true,\"mode\":\"use\",\"instanceId\":\"" + wantedId.ToString(inv) + "\""
		+ ",\"type\":\"" + definitionId + "\""
		+ ",\"used\":" + (usable ? "true" : "false")
		+ ",\"liquidBefore\":\"" + targetLiquid + "\""
		+ ",\"liquidAfter\":\"" + afterLiquid + "\""
		+ ",\"condition\":" + afterCondition
		+ ",\"alive\":" + (inScene ? "true" : "false") + "}";
}))()
