// recipe: carried-registration
// args: mode=s guest=s
// serves: carried-inventory-registration-re-report, guest-container-contents-ghost-drops-on-host,
//         validation-and-source-checks row 5 (the window a report is sequenced inside)
// returns: ok, mode, guest, due, burstBefore, burstAfter, burstRemaining, bodyPresent, generating,
//          memberCandidates, memberHandshaken, memberInWorld, judged, countBefore, countAfter,
//          entryIds, entries, error, detail
//
// The carried-inventory registration seam, read and steered from inside one client:
//  - mode=read (host): the guest's transfer-table entries (ItemArbitration.
//    GetTransferredItems) with each id's IsTransferredToGuest guard (the exact guard
//    the arbitration seam asks) and its kernel location — the record the re-report
//    must rebuild.
//  - mode=clear (host): removes one guest's entry map from the transfer table through
//    the evaluator. This is the declared substitution for a swallowed registration
//    frame (docs/evidence/acceptance/20260930-e-scope.md, Run B): the kernel facts are
//    left untouched, which is exactly the state the fix heals from.
//  - mode=arm (guest): opens the registration window through the public item control
//    and reports whether the cadence wants a report now. Used only when the natural
//    dense window has already closed; the record says so when it is.
//  - mode=status (either role): the window's own state, in the two facts the staging
//    turns on — the guest's cadence (due, remaining dense reports) beside the local
//    body and generation flag, and on the host the fact the kernel branches on:
//    whether this host has judged that member's carried-set statement
//    (ICarriedRegistrationFacts), with the member's presence row. A guest that holds an
//    item and a host that has not judged its statement is row 5's window.
//  - mode=freeze (guest): stops the cadence for one step by calling the registration
//    schedule's own Reset, so the run can put a report AHEAD of the statement instead
//    of racing it. This is a named substitution, not a product path: the record says
//    the window was held back, and mode=arm re-opens the same window through the
//    public item control.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var mode = {{s:mode}};
	var guestArg = {{s:guest}};
	if (mode != "read" && mode != "clear" && mode != "arm" && mode != "status" && mode != "freeze") {
		return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be read, clear, arm, status or freeze\"}";
	}
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.SessionService)) as CasualtiesUnknownOnline.Runtime.Session.SessionService;
	if (session == null) { return "{\"ok\":false,\"error\":\"no-session\"}"; }
	var guestId = 0UL;
	if (guestArg != "auto") {
		ulong.TryParse(guestArg, out guestId);
	}
	if (mode == "status" || mode == "freeze") {
		var inv = System.Globalization.CultureInfo.InvariantCulture;
		var coordinator = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Items.ItemIdCoordinator)) as CasualtiesUnknownOnline.Runtime.Session.Items.ItemIdCoordinator;
		if (coordinator == null) { return "{\"ok\":false,\"error\":\"no-id-coordinator\"}"; }
		var scheduleField = coordinator.GetType().GetField("_carriedRegistration", instanceFlags);
		if (scheduleField == null) { return "{\"ok\":false,\"error\":\"no-registration-schedule\",\"detail\":\"ItemIdCoordinator no longer carries _carriedRegistration\"}"; }
		var schedule = scheduleField.GetValue(coordinator);
		if (schedule == null) { return "{\"ok\":false,\"error\":\"no-registration-schedule\"}"; }
		var burstProperty = schedule.GetType().GetProperty("BurstRemaining", instanceFlags);
		if (burstProperty == null) { return "{\"ok\":false,\"error\":\"no-burst-reading\",\"detail\":\"CarriedInventoryReportSchedule no longer exposes BurstRemaining\"}"; }
		var burstBefore = System.Convert.ToInt32(burstProperty.GetValue(schedule, null), inv);
		if (mode == "freeze") {
			if (session.Role != CasualtiesUnknownOnline.Runtime.Session.SessionRole.Guest) {
				return "{\"ok\":false,\"error\":\"not-guest\",\"detail\":\"the registration window belongs to a guest client\"}";
			}
			var reset = schedule.GetType().GetMethod("Reset", instanceFlags);
			if (reset == null) { return "{\"ok\":false,\"error\":\"freeze-unavailable\",\"detail\":\"CarriedInventoryReportSchedule no longer offers Reset\"}"; }
			reset.Invoke(schedule, null);
			var itemsAfter = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Items.IItemControl)) as CasualtiesUnknownOnline.Runtime.Session.Items.IItemControl;
			var dueAfter = itemsAfter != null && itemsAfter.IsCarriedInventoryRegistrationDue();
			return "{\"ok\":true,\"mode\":\"freeze\",\"guest\":\"" + guestId.ToString(inv) + "\""
				+ ",\"burstBefore\":" + burstBefore.ToString(inv)
				+ ",\"burstAfter\":" + System.Convert.ToInt32(burstProperty.GetValue(schedule, null), inv).ToString(inv)
				+ ",\"dueAfter\":" + (dueAfter ? "true" : "false") + "}";
		}
		var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
		var gameWorld = WorldGeneration.world;
		var generating = false;
		if (gameWorld != null) {
			var generatingField = gameWorld.GetType().GetField("generatingWorld", instanceFlags);
			if (generatingField != null && generatingField.FieldType == typeof(bool)) {
				generating = (bool)generatingField.GetValue(gameWorld);
			}
		}
		var items = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Items.IItemControl)) as CasualtiesUnknownOnline.Runtime.Session.Items.IItemControl;
		var statusJson = new System.Text.StringBuilder();
		statusJson.Append("{\"ok\":true,\"mode\":\"status\",\"role\":\"").Append(session.Role.ToString()).Append("\"");
		statusJson.Append(",\"guest\":\"").Append(guestId.ToString(inv)).Append("\"");
		statusJson.Append(",\"localSteamId\":\"").Append(session.LocalSteamId.ToString(inv)).Append("\"");
		statusJson.Append(",\"sessionActive\":").Append(session.SessionActive ? "true" : "false");
		statusJson.Append(",\"bodyPresent\":").Append(body != null ? "true" : "false");
		statusJson.Append(",\"generating\":").Append(generating ? "true" : "false");
		statusJson.Append(",\"due\":").Append(items != null && items.IsCarriedInventoryRegistrationDue() ? "true" : "false");
		statusJson.Append(",\"burstRemaining\":").Append(burstBefore.ToString(inv));
		if (session.Role == CasualtiesUnknownOnline.Runtime.Session.SessionRole.Host) {
			var facts = services.GetService(typeof(CasualtiesUnknownOnline.Application.Kernel.ICarriedRegistrationFacts)) as CasualtiesUnknownOnline.Application.Kernel.ICarriedRegistrationFacts;
			var member = 0UL;
			var candidates = 0;
			var handshaken = false;
			var inWorld = false;
			foreach (var presence in session.Members) {
				if (presence.SteamId == session.LocalSteamId) { continue; }
				candidates++;
				member = presence.SteamId;
				handshaken = presence.Handshaken;
				inWorld = presence.InWorld;
			}
			statusJson.Append(",\"memberSteamId\":\"").Append(member.ToString(inv)).Append("\"");
			statusJson.Append(",\"memberCandidates\":").Append(candidates.ToString(inv));
			statusJson.Append(",\"memberHandshaken\":").Append(handshaken ? "true" : "false");
			statusJson.Append(",\"memberInWorld\":").Append(inWorld ? "true" : "false");
			statusJson.Append(",\"judged\":").Append(facts != null && member != 0UL && facts.HasJudgedCarriedSet(member) ? "true" : "false");
		}
		statusJson.Append("}");
		return statusJson.ToString();
	}
	if (mode == "arm") {
		if (session.Role != CasualtiesUnknownOnline.Runtime.Session.SessionRole.Guest) {
			return "{\"ok\":false,\"error\":\"not-guest\",\"detail\":\"the registration window belongs to a guest client\"}";
		}
		var items = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Items.IItemControl)) as CasualtiesUnknownOnline.Runtime.Session.Items.IItemControl;
		if (items == null) { return "{\"ok\":false,\"error\":\"no-item-control\"}"; }
		items.ArmCarriedInventoryRegistration("acceptance substitution: the run opens the registration window");
		var due = items.IsCarriedInventoryRegistrationDue();
		return "{\"ok\":true,\"mode\":\"arm\",\"guest\":\"" + guestId + "\",\"due\":" + (due ? "true" : "false") + "}";
	}
	if (session.Role != CasualtiesUnknownOnline.Runtime.Session.SessionRole.Host) {
		return "{\"ok\":false,\"error\":\"not-host\",\"detail\":\"the transfer table is read and cleared on the host\"}";
	}
	if (guestId == 0UL) {
		var candidates = 0;
		foreach (var member in session.Members) {
			if (member.SteamId != session.LocalSteamId && member.InWorld) { candidates++; guestId = member.SteamId; }
		}
		if (candidates != 1) {
			return "{\"ok\":false,\"error\":\"ambiguous-guest\",\"detail\":\"pass the guest SteamId explicitly\"}";
		}
	}
	var arbitration = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Items.ItemArbitration)) as CasualtiesUnknownOnline.Runtime.Session.Items.ItemArbitration;
	if (arbitration == null) { return "{\"ok\":false,\"error\":\"no-arbitration\"}"; }
	var before = arbitration.GetTransferredItems(guestId);
	var sb = new System.Text.StringBuilder();
	sb.Append("{\"ok\":true,\"mode\":\"").Append(mode).Append("\"");
	sb.Append(",\"guest\":\"").Append(guestId).Append("\"");
	sb.Append(",\"countBefore\":").Append(before.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
	sb.Append(",\"entryIds\":[");
	for (var i = 0; i < before.Count; i++) {
		if (i > 0) { sb.Append(","); }
		sb.Append("\"").Append(before[i].ItemId).Append("\"");
	}
	sb.Append("]");
	if (mode == "read") {
		var kernel = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Items.ItemKernelAuthority)) as CasualtiesUnknownOnline.Runtime.Session.Items.ItemKernelAuthority;
		sb.Append(",\"entries\":[");
		for (var i = 0; i < before.Count; i++) {
			if (i > 0) { sb.Append(","); }
			var entry = before[i];
			sb.Append("{\"id\":\"").Append(entry.ItemId).Append("\"");
			sb.Append(",\"type\":\"").Append(entry.Item.ItemId).Append("\"");
			sb.Append(",\"isTransferred\":").Append(arbitration.IsTransferredToGuest(guestId, entry.ItemId) ? "true" : "false");
			var kind = "unknown";
			if (kernel != null) {
				var kernelItems = kernel.QueryItems();
				if (kernelItems.ContainsKey(entry.ItemId)) {
					kind = kernelItems[entry.ItemId].Location.Kind.ToString();
				}
			}
			sb.Append(",\"kernel\":\"").Append(kind).Append("\"");
			var ids = new System.Collections.Generic.List<ulong>();
			var stack = new System.Collections.Generic.Stack<CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterItemMsg>();
			stack.Push(entry.Item);
			while (stack.Count > 0) {
				var current = stack.Pop();
				for (var c = 0; c < current.Contents.Count; c++) {
					ids.Add(current.Contents[c].InstanceId);
					stack.Push(current.Contents[c]);
				}
			}
			sb.Append(",\"contents\":[");
			for (var c = 0; c < ids.Count; c++) {
				if (c > 0) { sb.Append(","); }
				sb.Append("\"").Append(ids[c]).Append("\"");
			}
			sb.Append("]}");
		}
		sb.Append("]");
	}
	else {
		var field = typeof(CasualtiesUnknownOnline.Runtime.Session.Items.ItemArbitration).GetField("_transferred", instanceFlags);
		if (field == null) { return "{\"ok\":false,\"error\":\"clear-unavailable\",\"detail\":\"the transfer-table field was not found\"}"; }
		var map = field.GetValue(arbitration) as System.Collections.IDictionary;
		if (map == null) { return "{\"ok\":false,\"error\":\"clear-unavailable\",\"detail\":\"the transfer table is not an IDictionary\"}"; }
		map.Remove(guestId);
		var after = arbitration.GetTransferredItems(guestId);
		sb.Append(",\"countAfter\":").Append(after.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}
	sb.Append("}");
	return sb.ToString();
}))()
