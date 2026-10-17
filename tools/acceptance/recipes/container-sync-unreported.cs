// recipe: container-sync-unreported
// args: container=s item=s
// serves: member-report-bounds (the container-sync creation arms: a parent container and a content item
//         the host has never judged)
// returns: ok, containerType, itemType, containerId, itemId, containerSlot, freeSlots, canHold, distance,
//          loaded, held, contents, contentsCount, childLiquid, childCondition, containerCondition, depth;
//          on failure ok, error (plus detail where the code carries one)
//
// The forged-member half of the container-sync acceptance row: this client ends up CARRYING a container and,
// inside it, a content item, both with instance ids the host has never judged — the state a modified client
// is in when it invents a container locally and reports the container fact (`WireCommandKind.ItemContainerSync`)
// instead of reporting what it carries. `container` and `item` are item definition ids (the game's own
// `Item.GlobalItems` keys); `container` must be a definition whose prefab carries a `Container` (a `trashbag`),
// `item` any carried item that fits it (a `waterbottle` — its own liquid is the STATE the report must land).
//
// Two product rules do the work, no patch and no hand-picked id — the same pair the S3a recipe
// (`item-hold-unreported`) uses for its one item:
//   · Both items are created through the game's own grant (`Utils.Create` + `Body.PickUpItem(force)`) INSIDE an
//     inventory-internal scope (the GameAdapter's `CallContext.Origin.InternalReorder`), which is what decides
//     that a pickup is an inventory reorder and not a world event — the pickup report the game would have sent
//     is suppressed by that classification.
//   · `Item.Start` allocates an id and reports a spawn only for a STANDALONE WORLD item, and Start fires on the
//     next frame: an item already in a body when Start runs is never reported as a spawn. Both ids are stamped
//     here by the PRODUCT'S OWN allocator (`ItemIdAllocator.EnsureId`), the one id source a client owns.
//   · The content goes into the container OUTSIDE that scope, through the game's own `Container.LoadItem` — the
//     drag-UI entry the CUO container patches hook — so the load travels the real report path
//     (`ContainerItemSync`'s body-side branch): ONE `ItemContainerSync` naming the container and its full
//     recursive contents. The recipe never writes a parent by hand.
//
// Two evaluator constraints shape this file, and both are the REPL's, not the product's: no inner lambda captures
// an enclosing local (everything is inlined instead of written as a helper), and the internal scope is released
// BEFORE any later statement, so a throw from the game's own creation call cannot leave the scope open (the
// adapter's frame boundary is the backstop).
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags statics = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var containerType = {{s:container}};
	var itemType = {{s:item}};
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl)) as CasualtiesUnknownOnline.Runtime.Session.ISessionControl;
	if (session == null) { return "{\"ok\":false,\"error\":\"no-session\"}"; }
	if (session.Role != CasualtiesUnknownOnline.Runtime.Session.SessionRole.Guest) {
		return "{\"ok\":false,\"error\":\"not-guest\",\"detail\":\"an unreported container is a MEMBER's silence; run this recipe on the member, not the host\"}";
	}
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	if (!Item.GlobalItems.ContainsKey(containerType)) { return "{\"ok\":false,\"error\":\"no-definition\",\"detail\":\"Item.GlobalItems carries no definition for " + containerType + "\"}"; }
	if (!Item.GlobalItems.ContainsKey(itemType)) { return "{\"ok\":false,\"error\":\"no-definition\",\"detail\":\"Item.GlobalItems carries no definition for " + itemType + "\"}"; }
	// Two carried items, so two free slots are the precondition: a body that cannot hold both would leave one of
	// them a standalone world item, whose Start WOULD report a spawn and the host would know the id.
	var freeSlots = 0;
	for (var slot = 0; slot < body.slots.Length; slot++) {
		if (body.slots[slot] != null && body.slots[slot].canPickUp && !body.HoldingItem(slot)) { freeSlots++; }
	}
	if (freeSlots < 2) {
		return "{\"ok\":false,\"error\":\"not-enough-free-slots\",\"detail\":\"the container and its content are two carried items; this body has "
			+ freeSlots.ToString(inv) + " free slot(s)";
	}
	var contextType = System.Type.GetType("CasualtiesUnknownOnline.GameAdapter.CallContext, CasualtiesUnknownOnline.GameAdapter");
	if (contextType == null) { return "{\"ok\":false,\"error\":\"no-call-context\",\"detail\":\"the GameAdapter's CallContext is not loaded in this process\"}"; }
	var originType = contextType.GetNestedType("Origin", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
	var enter = contextType.GetMethod("Enter", statics);
	if (originType == null || enter == null) { return "{\"ok\":false,\"error\":\"no-call-context\",\"detail\":\"CallContext no longer offers Enter(Origin)\"}"; }
	var scope = enter.Invoke(null, new object[] { System.Enum.Parse(originType, "InternalReorder") }) as System.IDisposable;
	if (scope == null) {
		return "{\"ok\":false,\"error\":\"no-call-context\",\"detail\":\"Enter(InternalReorder) returned no scope, so a creation here would be REPORTED\"}";
	}
	Item containerItem = null;
	Item contentItem = null;
	var containerSlot = -1;
	var spawnedContainer = Utils.Create(containerType, body.transform.position, 0f);
	if (spawnedContainer != null) {
		containerItem = spawnedContainer.GetComponent<Item>();
		if (containerItem == null) { UnityEngine.Object.Destroy(spawnedContainer); }
		else {
			for (var slot = 0; slot < body.slots.Length; slot++) {
				if (body.slots[slot] != null && body.slots[slot].canPickUp && !body.HoldingItem(slot)) { containerSlot = slot; break; }
			}
			body.PickUpItem(containerItem, containerSlot, true);
			if (!body.HoldingItem(containerItem)) { containerItem = null; }
		}
	}
	var spawnedContent = Utils.Create(itemType, body.transform.position, 0f);
	if (spawnedContent != null) {
		contentItem = spawnedContent.GetComponent<Item>();
		if (contentItem == null) { UnityEngine.Object.Destroy(spawnedContent); }
		else {
			var contentSlot = -1;
			for (var slot = 0; slot < body.slots.Length; slot++) {
				if (body.slots[slot] != null && body.slots[slot].canPickUp && !body.HoldingItem(slot)) { contentSlot = slot; break; }
			}
			body.PickUpItem(contentItem, contentSlot, true);
			if (!body.HoldingItem(contentItem)) { contentItem = null; }
		}
	}
	scope.Dispose();
	if (containerItem == null) {
		return "{\"ok\":false,\"error\":\"hold-failed\",\"detail\":\"the game's own creation or pickup did not leave a " + containerType + " in the body\"}";
	}
	if (contentItem == null) {
		return "{\"ok\":false,\"error\":\"hold-failed\",\"detail\":\"the game's own creation or pickup did not leave a " + itemType + " in the body\"}";
	}
	var containerComp = containerItem.GetComponent<Container>();
	if (containerComp == null) {
		return "{\"ok\":false,\"error\":\"not-a-container\",\"detail\":\"" + containerType + " carries no Container component, so no container fact could be reported\"}";
	}
	// The product's own allocator: PatchBridge.Impl -> GameAdapterBridge's domains -> the item id allocator. The
	// domain field is the bridge's primary-constructor capture, so it is matched by its TYPE, not by a name the
	// compiler chose.
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
	var containerId = System.Convert.ToUInt64(ensureId.Invoke(allocator, new object[] { containerItem }));
	if (containerId == 0UL) { return "{\"ok\":false,\"error\":\"no-instance-id\",\"detail\":\"the product's allocator refused to stamp an id on the created container\"}"; }
	var contentId = System.Convert.ToUInt64(ensureId.Invoke(allocator, new object[] { contentItem }));
	if (contentId == 0UL) { return "{\"ok\":false,\"error\":\"no-instance-id\",\"detail\":\"the product's allocator refused to stamp an id on the created content item\"}"; }
	// The precondition the run needs: the game's own capacity and the distance LoadItem requires (< 10 units).
	// Read BEFORE the load — the loaded child changes the container's holding weight.
	var canHold = containerComp.CanHoldItem(contentItem);
	var distance = UnityEngine.Vector2.Distance(contentItem.transform.position, containerComp.transform.position);
	containerComp.LoadItem(contentItem);
	var loaded = contentItem.transform.parent == containerComp.transform;
	if (!loaded) {
		return "{\"ok\":false,\"error\":\"load-refused\",\"detail\":\"the game's own Container.LoadItem left " + itemType + " outside the "
			+ containerType + " (canHold=" + (canHold ? "true" : "false") + ", distance="
			+ distance.ToString("0.###", inv) + "), so no container fact was reported\"}";
	}
	var contentsCount = 0;
	var contents = new System.Text.StringBuilder();
	for (var child = 0; child < containerComp.transform.childCount; child++) {
		var childItem = containerComp.transform.GetChild(child).GetComponent<Item>();
		if (childItem == null) { continue; }
		if (contentsCount > 0) { contents.Append(","); }
		contentsCount++;
		var childId = 0UL;
		var childComponents = childItem.GetComponents<UnityEngine.Component>();
		for (var c = 0; c < childComponents.Length; c++) {
			if (childComponents[c] == null || childComponents[c].GetType().Name != "ItemInstanceId") { continue; }
			// The marker exposes `Id` as a PROPERTY over a private field; the field arm is the fallback, checked
			// the same way `container-fill` does, because a field-only read answers 0 and looks like a missing id.
			var childIdProperty = childComponents[c].GetType().GetProperty("Id", instanceFlags);
			if (childIdProperty != null) { childId = System.Convert.ToUInt64(childIdProperty.GetValue(childComponents[c], null)); }
			else {
				var childIdField = childComponents[c].GetType().GetField("Id", instanceFlags);
				if (childIdField != null) { childId = System.Convert.ToUInt64(childIdField.GetValue(childComponents[c])); }
			}
			break;
		}
		contents.Append("{\"type\":\"").Append(childItem.id).Append("\",\"id\":\"").Append(childId.ToString(inv)).Append("\"}");
	}
	var childWater = contentItem.GetComponent<WaterContainerItem>();
	var childLiquid = "none";
	if (childWater != null) {
		var childBuilder = new System.Text.StringBuilder();
		for (var k = 0; k < childWater.stack.Count; k++) {
			if (k > 0) { childBuilder.Append(","); }
			childBuilder.Append(childWater.stack[k].liquidId).Append("=").Append(childWater.stack[k].amount.ToString("0.###", inv));
		}
		childLiquid = childBuilder.ToString();
	}
	var depthField = contextType.GetField("_depth", statics);
	return "{\"ok\":true,\"containerType\":\"" + containerType + "\""
		+ ",\"itemType\":\"" + itemType + "\""
		+ ",\"containerId\":\"" + containerId.ToString(inv) + "\""
		+ ",\"itemId\":\"" + contentId.ToString(inv) + "\""
		+ ",\"containerSlot\":" + containerSlot.ToString(inv)
		+ ",\"freeSlots\":" + freeSlots.ToString(inv)
		+ ",\"canHold\":" + (canHold ? "true" : "false")
		+ ",\"distance\":" + distance.ToString("0.###", inv)
		+ ",\"loaded\":true"
		+ ",\"held\":" + (contentItem.GetComponentInParent<Body>() != null ? "true" : "false")
		+ ",\"contents\":[" + contents.ToString() + "]"
		+ ",\"contentsCount\":" + contentsCount.ToString(inv)
		+ ",\"childLiquid\":\"" + childLiquid + "\""
		+ ",\"childCondition\":" + contentItem.condition.ToString("0.###", inv)
		+ ",\"containerCondition\":" + containerItem.condition.ToString("0.###", inv)
		+ ",\"depth\":" + (depthField == null ? -1 : System.Convert.ToInt32(depthField.GetValue(null))) + "}";
}))()
