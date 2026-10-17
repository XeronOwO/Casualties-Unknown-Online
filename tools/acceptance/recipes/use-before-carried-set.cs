// recipe: use-before-carried-set
// args: mode=s id=s condition=s
// serves: validation-and-source-checks row 5 (a report arriving before its creation claim is accepted in
//         the documented order, not refused)
// returns: mode=apply -> ok, mode, instanceId, type, usable, held, localCondition, appliedCondition, recorded;
//          mode=report -> ok, mode, instanceId, type, usable, localCondition, condition, recorded, sent;
//          on failure ok=false plus error (and detail where the code carries one)
//
// The REPORT half of row 5, staged inside the window where this host has not judged the member's
// carried-set statement yet: the heal that window exists for treats the member's first item report as
// the creation itself, so the host must materialize the carried fact instead of refusing it.
//
//   apply  - writes the post-use state a modified client puts on its own object before it reports it,
//            with NOTHING sent. The run reads the object back in its own step, so "the local object
//            held the applied state" is a reading and not the recipe's own claim.
//   report - sends ONE WireCommand of kind ItemUse carrying exactly that state through
//            IKernelProtocolControl.SendCommand (the same frame the product's own sender emits, built
//            by hand). It refuses unless this body still holds the item at the value apply wrote, so
//            the frame can never carry a state the local object does not have.
//
// Unlike item-use-forge, this recipe REQUIRES no local kernel row for the id: that absence is the
// point. The id is a fact only this client knows (the item was created and stamped behind the
// product's own suppressed pickup), and the report is the item's only claim on the host - the
// accepted-first materialization row 5 reads. The recipe refuses on the host for the same reason: the
// report is the member's.
//
// Two evaluator constraints shape this file, and both are the REPL's, not the product's: no inner lambda
// captures an enclosing local (everything is inlined), and a reflectively reached member is read through
// its PROPERTY when one exists (a field-only read answers the CLR default, see tools/AGENTS.md).
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags statics = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var mode = {{s:mode}};
	var rawId = {{s:id}};
	var rawCondition = {{s:condition}};
	if (mode != "apply" && mode != "report") {
		return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be apply or report\"}";
	}
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl)) as CasualtiesUnknownOnline.Runtime.Session.ISessionControl;
	if (session == null) { return "{\"ok\":false,\"error\":\"no-session\"}"; }
	if (session.Role != CasualtiesUnknownOnline.Runtime.Session.SessionRole.Guest) {
		return "{\"ok\":false,\"error\":\"not-guest\",\"detail\":\"the use report is the MEMBER's; run this on the member, not the host\"}";
	}
	if (!session.SessionActive) { return "{\"ok\":false,\"error\":\"no-active-session\"}"; }
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var wantedId = 0UL;
	if (!ulong.TryParse(rawId, System.Globalization.NumberStyles.None, inv, out wantedId) || wantedId == 0UL) {
		return "{\"ok\":false,\"error\":\"bad-id\",\"detail\":\"id must be a non-zero decimal item instance id\"}";
	}
	var condition = 0f;
	if (!float.TryParse(rawCondition, System.Globalization.NumberStyles.Float, inv, out condition)) {
		return "{\"ok\":false,\"error\":\"bad-condition\",\"detail\":\"condition must be a decimal value in the item's own range (e.g. 0.62)\"}";
	}
	if (condition < 0f || condition > 1f) {
		return "{\"ok\":false,\"error\":\"bad-condition\",\"detail\":\"condition must lie in 0..1; the game's own condition is the fraction of full\"}";
	}
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
	Item target = null;
	for (var i = 0; i < carried.Count; i++) {
		var marker = carried[i].GetComponent(markerType);
		if (marker == null) { continue; }
		var idProperty = marker.GetType().GetProperty("Id", instanceFlags);
		if (idProperty == null) { continue; }
		if (System.Convert.ToUInt64(idProperty.GetValue(marker, null), inv) == wantedId) { target = carried[i]; break; }
	}
	if (target == null) { return "{\"ok\":false,\"error\":\"not-carried\",\"detail\":\"this body holds no item #" + wantedId.ToString(inv) + "\"}"; }
	if (!body.HoldingItem(target)) { return "{\"ok\":false,\"error\":\"not-held\",\"detail\":\"item #" + wantedId.ToString(inv) + " is not in this body's hands\"}"; }
	var definitionId = target.id;
	var usable = target.Stats != null && target.Stats.usable;
	var facts = services.GetService(typeof(CasualtiesUnknownOnline.Application.Kernel.IKernelItemFacts)) as CasualtiesUnknownOnline.Application.Kernel.IKernelItemFacts;
	var kernelRow = facts == null ? (CasualtiesUnknownOnline.GameState.Domains.Items.ItemState?)null : facts.FindItem(wantedId);
	if (mode == "apply") {
		// The already-applied use, and NOTHING sent: the run reads the local object back before reporting.
		target.condition = condition;
		return "{\"ok\":true,\"mode\":\"apply\",\"instanceId\":\"" + wantedId.ToString(inv) + "\""
			+ ",\"type\":\"" + definitionId + "\""
			+ ",\"usable\":" + (usable ? "true" : "false")
			+ ",\"held\":true"
			+ ",\"localCondition\":" + target.condition.ToString("0.###", inv)
			+ ",\"appliedCondition\":" + condition.ToString("0.###", inv)
			+ ",\"recorded\":" + (kernelRow == null ? "false" : "true") + "}";
	}
	if (System.Math.Abs(target.condition - condition) >= 0.0001f) {
		return "{\"ok\":false,\"error\":\"local-condition-differs\",\"detail\":\"the local object holds condition " + target.condition.ToString("0.###", inv) + ", not the reported " + condition.ToString("0.###", inv) + " - run apply with the same value first, or report what the object holds\"}";
	}
	var kernel = services.GetService(typeof(CasualtiesUnknownOnline.Application.Kernel.IKernelProtocolControl)) as CasualtiesUnknownOnline.Application.Kernel.IKernelProtocolControl;
	if (kernel == null) { return "{\"ok\":false,\"error\":\"no-kernel-protocol\"}"; }
	var heldSlot = -1;
	for (var slot = 0; slot < body.slots.Length; slot++) {
		if (body.GetItem(slot) == target) { heldSlot = slot; break; }
	}
	var reportMsg = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterItemMsg();
	reportMsg.InstanceId = wantedId;
	reportMsg.ItemId = definitionId;
	reportMsg.Condition = condition;
	reportMsg.SlotIndex = heldSlot;
	var reportIdentity = new CasualtiesUnknownOnline.Protocol.Wire.WireItemIdentity();
	reportIdentity.InstanceId = wantedId;
	reportIdentity.DefinitionId = definitionId;
	var reportCommand = new CasualtiesUnknownOnline.Protocol.Wire.WireCommand();
	reportCommand.Kind = CasualtiesUnknownOnline.Protocol.Wire.WireCommandKind.ItemUse;
	reportCommand.Identity = reportIdentity;
	reportCommand.Data = CasualtiesUnknownOnline.Application.Kernel.KernelWireMapper.ToWireData(
		CasualtiesUnknownOnline.Runtime.Session.Items.ItemKernelAuthority.ToKernelData(reportMsg));
	kernel.SendCommand(reportCommand, CasualtiesUnknownOnline.Protocol.Wire.WirePayloadType.ItemUseCommand);
	return "{\"ok\":true,\"mode\":\"report\",\"instanceId\":\"" + wantedId.ToString(inv) + "\""
		+ ",\"type\":\"" + definitionId + "\""
		+ ",\"usable\":" + (usable ? "true" : "false")
		+ ",\"localCondition\":" + target.condition.ToString("0.###", inv)
		+ ",\"condition\":" + condition.ToString("0.###", inv)
		+ ",\"recorded\":" + (kernelRow == null ? "false" : "true")
		+ ",\"sent\":true"
		+ ",\"role\":\"Guest\""
		+ ",\"utc\":\"" + System.DateTime.UtcNow.ToString("o") + "\"}";
}))()
