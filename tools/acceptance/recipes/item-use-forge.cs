// recipe: item-use-forge
// args: mode=s type=s id=s condition=s
// serves: validation-and-source-checks row 7 (a member reports a native use of an item whose own data forbids one)
// returns: mode=hold -> ok, mode, type, instanceId, slot, condition, usable;
//          mode=forge -> ok, mode, instanceId, type, usable, recordedCondition, recordedRevision,
//                        forgedCondition, sent, utc;
//          mode=read -> ok, mode, instanceId, type, held, localCondition, localLiquid, recorded,
//                       recordedCondition, recordedLiquid, recordedKind, recordedOwner, recordedRevision;
//          mode=apply -> ok, mode, instanceId, type, usable, recordedCondition, appliedCondition, sent;
//          on failure ok=false plus error (and detail where the code carries one)
//
// The forged-member half of row 7: this client holds an item and sends a NATIVE-USE report for it that
// no gesture of this client could have produced, because the item's own data says no native use exists
// (ItemInfo.usable is clear - a bandage is applied through the wound view, never through Body.UseItem,
// and both of the game's use call sites require that flag).
//
//   hold  - creates one item through the game's own grant (Utils.Create + Body.PickUpItem(force)) INSIDE
//           an inventory-internal scope, then stamps its instance id with the PRODUCT'S OWN allocator
//           (ItemIdAllocator.EnsureId) - the same two product rules item-hold-unreported relies on: the
//           scope is CUO's own call identity, so the pickup report is suppressed by classification, and
//           Item.Start allocates an id only for a standalone world item. The item is therefore a fact this
//           client holds and the host learns through the carried-set statement, which is what makes the
//           later use report one the host CAN judge (an id it never judged would be refused by the
//           creation-before-operation rule before the use bound is reached, which proves nothing).
//   apply - the same write forge's first step makes, with NOTHING sent: the post-use state a modified
//           client puts on its own object before it reports it. Its purpose is the reading a single eval
//           cannot give - the run applies the value, reads the local object back in its own step, and
//           only then forges the report with that value, so "the object really held the applied state"
//           and "the settlement put the recorded one back" are two readings rather than one claim.
//   forge - writes the post-use state a modified client would have applied onto its own object (the
//           run's declared substitution: no game call may run this use), then sends ONE WireCommand of
//           kind ItemUse carrying that state through IKernelProtocolControl.SendCommand - the same frame
//           ItemActionSync.SendItemUse emits, built by hand. The recipe refuses to forge unless this
//           client already RECORDS the id (the host's own row, replayed here) and the forged value differs
//           from the recorded one, so the settlement has something to put back and the reading is never
//           ambiguous. It reports the definition's own `usable` read from THIS client's registry as a
//           fact, never as the verdict - the verdict is the host's.
//   read  - one item's state by instance id, from the LOCAL body when it holds it AND from this client's
//           record when it holds one, in the same answer: `held`/`localCondition` are this body,
//           `recorded*` is the session's record (on the host, its own authority; on the member, the
//           host's committed fact replayed here). The settlement row needs both in one step.
//
// Two evaluator constraints shape this file, and both are the REPL's, not the product's: no inner lambda
// captures an enclosing local (everything is inlined), and the internal scope is released BEFORE any later
// statement, so a throw from the game's own creation call cannot leave it open.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags statics = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var mode = {{s:mode}};
	var type = {{s:type}};
	var rawId = {{s:id}};
	var rawCondition = {{s:condition}};
	if (mode != "hold" && mode != "forge" && mode != "read" && mode != "apply") {
		return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be hold, apply, forge or read\"}";
	}
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl)) as CasualtiesUnknownOnline.Runtime.Session.ISessionControl;
	if (session == null) { return "{\"ok\":false,\"error\":\"no-session\"}"; }
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
		if (session.Role != CasualtiesUnknownOnline.Runtime.Session.SessionRole.Guest) {
			return "{\"ok\":false,\"error\":\"not-guest\",\"detail\":\"the unreported item is a MEMBER's fact; run this on the member, not the host\"}";
		}
		if (!Item.GlobalItems.ContainsKey(type)) { return "{\"ok\":false,\"error\":\"no-definition\",\"detail\":\"Item.GlobalItems carries no definition for " + type + "\"}"; }
		var definition = Item.GlobalItems[type];
		if (definition == null) { return "{\"ok\":false,\"error\":\"no-definition\",\"detail\":\"Item.GlobalItems carries no definition for " + type + "\"}"; }
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
		var heldSlot = -1;
		for (var slot = 0; slot < body.slots.Length; slot++) {
			if (body.GetItem(slot) == held) { heldSlot = slot; break; }
		}
		return "{\"ok\":true,\"mode\":\"hold\",\"type\":\"" + type + "\""
			+ ",\"instanceId\":\"" + stampedId.ToString(inv) + "\""
			+ ",\"slot\":" + heldSlot.ToString(inv)
			+ ",\"condition\":" + held.condition.ToString("0.###", inv)
			+ ",\"usable\":" + (definition.usable ? "true" : "false") + "}";
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
	var facts = services.GetService(typeof(CasualtiesUnknownOnline.Application.Kernel.IKernelItemFacts)) as CasualtiesUnknownOnline.Application.Kernel.IKernelItemFacts;
	var kernelRow = facts == null ? (CasualtiesUnknownOnline.GameState.Domains.Items.ItemState?)null : facts.FindItem(wantedId);
	if (mode == "read") {
		var readType = target != null ? target.id : (kernelRow == null ? "" : kernelRow.Value.Identity.DefinitionId);
		var sb = new System.Text.StringBuilder();
		sb.Append("{\"ok\":true,\"mode\":\"read\",\"instanceId\":\"").Append(wantedId.ToString(inv)).Append("\"");
		sb.Append(",\"type\":\"").Append(readType).Append("\"");
		sb.Append(",\"held\":").Append(target != null && body.HoldingItem(target) ? "true" : "false");
		var localLiquid = "none";
		if (target != null) {
			var localWater = target.GetComponent<WaterContainerItem>();
			if (localWater != null) {
				var localBuilder = new System.Text.StringBuilder();
				for (var k = 0; k < localWater.stack.Count; k++) {
					if (k > 0) { localBuilder.Append(","); }
					localBuilder.Append(localWater.stack[k].liquidId).Append("=").Append(localWater.stack[k].amount.ToString("0.###", inv));
				}
				localLiquid = localBuilder.ToString();
			}
		}
		sb.Append(",\"localCondition\":").Append(target == null ? "-1" : target.condition.ToString("0.###", inv));
		sb.Append(",\"localLiquid\":\"").Append(localLiquid).Append("\"");
		if (kernelRow == null) {
			sb.Append(",\"recorded\":false,\"recordedCondition\":-1,\"recordedLiquid\":\"none\",\"recordedKind\":\"none\",\"recordedOwner\":\"0\",\"recordedRevision\":-1}");
			return sb.ToString();
		}
		var row = kernelRow.Value;
		var kernelLiquid = "";
		for (var i = 0; i < row.Data.Liquids.Count; i++) {
			if (i > 0) { kernelLiquid = kernelLiquid + ","; }
			kernelLiquid = kernelLiquid + row.Data.Liquids[i].LiquidId + "=" + row.Data.Liquids[i].Amount.ToString("0.###", inv);
		}
		sb.Append(",\"recorded\":true");
		sb.Append(",\"recordedCondition\":").Append(row.Data.Condition.ToString("0.###", inv));
		sb.Append(",\"recordedLiquid\":\"").Append(kernelLiquid).Append("\"");
		sb.Append(",\"recordedKind\":\"").Append(row.Location.Kind.ToString()).Append("\"");
		sb.Append(",\"recordedOwner\":\"").Append(row.Location.Owner.Value.ToString(inv)).Append("\"");
		sb.Append(",\"recordedRevision\":").Append(row.Revision.ToString(inv)).Append("}");
		return sb.ToString();
	}
	// forge
	if (session.Role != CasualtiesUnknownOnline.Runtime.Session.SessionRole.Guest) {
		return "{\"ok\":false,\"error\":\"not-guest\",\"detail\":\"the use report is the MEMBER's; run this on the member, not the host\"}";
	}
	if (target == null) { return "{\"ok\":false,\"error\":\"not-carried\",\"detail\":\"this body holds no item #" + wantedId.ToString(inv) + "\"}"; }
	if (kernelRow == null) {
		return "{\"ok\":false,\"error\":\"unjudged\",\"detail\":\"this client records no row for #" + wantedId.ToString(inv) + " - the host would refuse the report for that reason, not for the use claim; let the carried-set statement land it first\"}";
	}
	var recorded = kernelRow.Value;
	var forged = 0f;
	if (!float.TryParse(rawCondition, System.Globalization.NumberStyles.Float, inv, out forged)) {
		return "{\"ok\":false,\"error\":\"bad-condition\",\"detail\":\"condition must be a decimal value in the item's own range (e.g. 0.35)\"}";
	}
	if (forged < 0f || forged > 1f) {
		return "{\"ok\":false,\"error\":\"bad-condition\",\"detail\":\"condition must lie in 0..1; the game's own condition is the fraction of full\"}";
	}
	if (System.Math.Abs(forged - recorded.Data.Condition) < 0.0001f) {
		return "{\"ok\":false,\"error\":\"condition-not-different\",\"detail\":\"the forged value equals the recorded one, so a settlement would be invisible; pick a different value\"}";
	}
	var kernel = services.GetService(typeof(CasualtiesUnknownOnline.Application.Kernel.IKernelProtocolControl)) as CasualtiesUnknownOnline.Application.Kernel.IKernelProtocolControl;
	if (kernel == null) { return "{\"ok\":false,\"error\":\"no-kernel-protocol\"}"; }
	var definitionId = target.id;
	var usable = target.Stats != null && target.Stats.usable;
	if (mode == "apply") {
		// The already-applied use, and NOTHING sent: the run reads the local object back before forging.
		target.condition = forged;
		return "{\"ok\":true,\"mode\":\"apply\",\"instanceId\":\"" + wantedId.ToString(inv) + "\""
			+ ",\"type\":\"" + definitionId + "\""
			+ ",\"usable\":" + (usable ? "true" : "false")
			+ ",\"recordedCondition\":" + recorded.Data.Condition.ToString("0.###", inv)
			+ ",\"appliedCondition\":" + forged.ToString("0.###", inv)
			+ ",\"sent\":false}";
	}
	var forgedSlot = -1;
	for (var slot = 0; slot < body.slots.Length; slot++) {
		if (body.GetItem(slot) == target) { forgedSlot = slot; break; }
	}
	// The already-applied use: a modified client runs whatever it likes on its own object, then reports it.
	target.condition = forged;
	var forgedMsg = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterItemMsg();
	forgedMsg.InstanceId = wantedId;
	forgedMsg.ItemId = definitionId;
	forgedMsg.Condition = forged;
	forgedMsg.SlotIndex = forgedSlot;
	var forgedIdentity = new CasualtiesUnknownOnline.Protocol.Wire.WireItemIdentity();
	forgedIdentity.InstanceId = wantedId;
	forgedIdentity.DefinitionId = definitionId;
	var forgedCommand = new CasualtiesUnknownOnline.Protocol.Wire.WireCommand();
	forgedCommand.Kind = CasualtiesUnknownOnline.Protocol.Wire.WireCommandKind.ItemUse;
	forgedCommand.Identity = forgedIdentity;
	forgedCommand.Data = CasualtiesUnknownOnline.Application.Kernel.KernelWireMapper.ToWireData(
		CasualtiesUnknownOnline.Runtime.Session.Items.ItemKernelAuthority.ToKernelData(forgedMsg));
	kernel.SendCommand(forgedCommand, CasualtiesUnknownOnline.Protocol.Wire.WirePayloadType.ItemUseCommand);
	return "{\"ok\":true,\"mode\":\"forge\",\"instanceId\":\"" + wantedId.ToString(inv) + "\""
		+ ",\"type\":\"" + definitionId + "\""
		+ ",\"usable\":" + (usable ? "true" : "false")
		+ ",\"recordedCondition\":" + recorded.Data.Condition.ToString("0.###", inv)
		+ ",\"recordedRevision\":" + recorded.Revision.ToString(inv)
		+ ",\"forgedCondition\":" + forged.ToString("0.###", inv)
		+ ",\"sent\":true"
		+ ",\"role\":\"Guest\""
		+ ",\"utc\":\"" + System.DateTime.UtcNow.ToString("o") + "\"}";
}))()
