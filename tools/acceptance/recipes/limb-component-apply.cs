// recipe: limb-component-apply
// args: mode=s limb=n
// serves: medical-removal-limb-component-not-applied, display-clone-limb-component-runs-native-start
// returns: ok, mode, limb, componentsBefore, componentsAfter, splintedBefore, splintedAfter,
//          startRanBefore, startRanAfter, conditionBefore, conditionAfter, enabled, messageComponents, note
//          — the `status` mode answers every reading; `add-enabled`/`add-disabled` answer only what their
//          own frame knows (`enabled`, `condition`, `note`), because the game's `Start` has not run yet in
//          either case and a `startRan` field there would be a constant that can never fail.
//
// Drives the RECEIVING half of a limb-component change on the CALLER's own body, through the same
// production call the authority's medical-operation message takes on the target's client
// (`CharacterDataSync.ApplyHealState`, the generic branch of `MedicalOperationApply.ApplyProgress`).
// Nothing here fakes the path under test: the snapshot is the body's own capture, and only the one limb
// row is edited to the shape the authority sends.
//
// It exists because the splint-removal defect is invisible to every lighter check: the limb's FLAG lands
// (the mapper writes `Splinted`), so only the component list tells the truth about whether the removal
// happened, and the receiving path is in the Game Adapter — which the behavioural test project
// references with ExcludeAssets="compile", so no L0 case in this repository can reach it.
//
// Modes, one call each (a later call reads what an earlier one left, which is how the game's own frame
// timing is observed instead of being waited out):
//   mode=status        read the limb's own state: the component instances the limb carries, the splint
//                      flag, the component's `condition`, `enabled`, and whether the game's own
//                      `SplintLimb.Start` has run (its private `limb` field is what Start binds)
//   mode=add-enabled   add the game's own `SplintLimb` the way the game's splint application does and
//                      leave it ENABLED — the control for the pair below
//   mode=add-disabled  add the same component with `enabled = false` — the inert-state shape a
//                      display-only body needs if the panel must still read the component
//   mode=message-clear the authority's own removal shape: the limb row carries NO components and
//                      `Splinted=false`, exactly what `OtherMedicalRemovalApplier` leaves in its stored
//                      copy. Applied through `ApplyHealState`. A limb that still carries the component
//                      afterwards is the removal that never landed
//   mode=drop          destroy the limb's component instances and clear the flag (probe cleanup: the
//                      caller's own body is real state)
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var limbIndex = (int){{n:limb}};
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var num = new System.Func<float, string>((value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));

	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	if (limbIndex < 0 || limbIndex >= body.limbs.Length) { return "{\"ok\":false,\"error\":\"limb-out-of-range\",\"detail\":\"limb " + limbIndex + " of " + body.limbs.Length + "\"}"; }
	var limb = body.limbs[limbIndex];
	if (limb == null) { return "{\"ok\":false,\"error\":\"no-such-limb\",\"detail\":\"limb " + limbIndex + "\"}"; }

	var startField = typeof(SplintLimb).GetField("limb", flags);

	var componentsBefore = "";
	var componentsAfter = "";
	var splintedBefore = false;
	var splintedAfter = false;
	var startRanBefore = false;
	var startRanAfter = false;
	var conditionBefore = 0f;
	var conditionAfter = 0f;
	var enabledText = "n/a";
	var messageComponents = -1;

	if (mode == "status") {
		var splintNow = limb.GetComponent<SplintLimb>();
		if (splintNow != null) {
			componentsBefore = "SplintLimb";
			conditionBefore = splintNow.condition;
			enabledText = splintNow.enabled ? "true" : "false";
			try { startRanBefore = startField != null && startField.GetValue(splintNow) != null; } catch { startRanBefore = false; }
		}
		if (limb.GetComponent<TourniquetScript>() != null) { componentsBefore = componentsBefore.Length > 0 ? componentsBefore + ",TourniquetScript" : "TourniquetScript"; }
		if (limb.GetComponent<ChilledLimb>() != null) { componentsBefore = componentsBefore.Length > 0 ? componentsBefore + ",ChilledLimb" : "ChilledLimb"; }
		splintedBefore = limb.splinted;
		return "{\"ok\":true,\"mode\":\"status\",\"limb\":" + limbIndex
			+ ",\"componentsBefore\":\"" + componentsBefore + "\",\"componentsAfter\":\"" + componentsBefore + "\""
			+ ",\"splintedBefore\":" + (splintedBefore ? "true" : "false") + ",\"splintedAfter\":" + (splintedBefore ? "true" : "false")
			+ ",\"startRanBefore\":" + (startRanBefore ? "true" : "false") + ",\"startRanAfter\":" + (startRanBefore ? "true" : "false")
			+ ",\"conditionBefore\":" + num(conditionBefore) + ",\"conditionAfter\":" + num(conditionBefore)
			+ ",\"enabled\":\"" + enabledText + "\",\"messageComponents\":" + messageComponents + "}";
	}

	if (mode == "add-enabled" || mode == "add-disabled") {
		var splint = limb.GetComponent<SplintLimb>();
		if (splint == null) {
			splint = limb.gameObject.AddComponent<SplintLimb>();
			splint.conditionLossMinute = 0.015f;
			splint.condition = 100f;
			splint.item = "splint";
		}
		// The whole point of the pair: a display-only body must be able to carry the component WITHOUT
		// the game's own Start/Update running on it — `enabled` is the claim under test, and the READER is
		// the NEXT call's `mode=status`: in this frame the game's Start has not run for either case, so
		// this answer reports only what the frame knows (the instance and its flag) rather than a
		// `splinted`/`startRan` reading that would be false for both modes.
		splint.enabled = mode == "add-enabled";
		return "{\"ok\":true,\"mode\":\"" + mode + "\",\"limb\":" + limbIndex
			+ ",\"componentsBefore\":\"" + componentsBefore + "\",\"componentsAfter\":\"SplintLimb\""
			+ ",\"enabled\":\"" + (splint.enabled ? "true" : "false") + "\""
			+ ",\"condition\":" + num(splint.condition)
			+ ",\"note\":\"read this limb with mode=status in a later call: Start has not run in this frame\"}";
	}

	if (mode == "drop") {
		var splintDrop = limb.GetComponent<SplintLimb>();
		var tourniquetDrop = limb.GetComponent<TourniquetScript>();
		var chilledDrop = limb.GetComponent<ChilledLimb>();
		if (splintDrop != null) { componentsBefore = "SplintLimb"; }
		if (tourniquetDrop != null) { componentsBefore = componentsBefore.Length > 0 ? componentsBefore + ",TourniquetScript" : "TourniquetScript"; }
		if (chilledDrop != null) { componentsBefore = componentsBefore.Length > 0 ? componentsBefore + ",ChilledLimb" : "ChilledLimb"; }
		if (splintDrop != null) { UnityEngine.Object.Destroy(splintDrop); }
		if (tourniquetDrop != null) { UnityEngine.Object.Destroy(tourniquetDrop); }
		if (chilledDrop != null) { UnityEngine.Object.Destroy(chilledDrop); }
		var dropped = limb.GetComponent<SplintLimb>();
		if (dropped != null) { componentsAfter = "SplintLimb"; }
		if (limb.GetComponent<TourniquetScript>() != null) { componentsAfter = componentsAfter.Length > 0 ? componentsAfter + ",TourniquetScript" : "TourniquetScript"; }
		if (limb.GetComponent<ChilledLimb>() != null) { componentsAfter = componentsAfter.Length > 0 ? componentsAfter + ",ChilledLimb" : "ChilledLimb"; }
		limb.splinted = false;
		limb.blockedBleeding = false;
		return "{\"ok\":true,\"mode\":\"drop\",\"limb\":" + limbIndex
			+ ",\"componentsBefore\":\"" + componentsBefore + "\",\"componentsAfter\":\"" + componentsAfter + "\""
			+ ",\"splintedBefore\":true,\"splintedAfter\":" + (limb.splinted ? "true" : "false")
			+ ",\"startRanBefore\":false,\"startRanAfter\":false"
			+ ",\"conditionBefore\":0,\"conditionAfter\":0"
			+ ",\"enabled\":\"n/a\",\"messageComponents\":-1}";
	}

	if (mode != "message-clear") { return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"" + mode + "\"}"; }

	// The production receiving half: the domain object is looked up by TYPE (a renamed field would
	// otherwise change what this probe drives without failing), and the sync coordinator by field.
	var bridgeType = (System.Type)null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length; i++) {
		try { bridgeType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.PatchBridge", false); } catch { bridgeType = null; }
		if (bridgeType != null) { break; }
	}
	if (bridgeType == null) { return "{\"ok\":false,\"error\":\"no-patch-bridge-type\"}"; }
	var implProp = bridgeType.GetProperty("Impl", flags);
	if (implProp == null) { return "{\"ok\":false,\"error\":\"no-impl-property\"}"; }
	var impl = implProp.GetValue(null, null);
	if (impl == null) { return "{\"ok\":false,\"error\":\"no-bridge-instance\"}"; }
	System.Reflection.FieldInfo domainsField = null;
	var bridgeFields = impl.GetType().GetFields(flags);
	for (var i = 0; i < bridgeFields.Length; i++) {
		if (bridgeFields[i].FieldType.Name == "GameAdapterDomains") { domainsField = bridgeFields[i]; break; }
	}
	if (domainsField == null) { return "{\"ok\":false,\"error\":\"no-domains-field\"}"; }
	var domains = domainsField.GetValue(impl);
	var syncField = domains.GetType().GetField("CharacterDataSync", flags);
	if (syncField == null) { return "{\"ok\":false,\"error\":\"no-character-data-sync-field\"}"; }
	var sync = syncField.GetValue(domains);
	if (sync == null) { return "{\"ok\":false,\"error\":\"no-character-data-sync\"}"; }
	var captureMethod = sync.GetType().GetMethod("CaptureLocal", flags);
	if (captureMethod == null) { return "{\"ok\":false,\"error\":\"no-capture-local\"}"; }
	var applyMethod = sync.GetType().GetMethod("ApplyHealState", flags);
	if (applyMethod == null) { return "{\"ok\":false,\"error\":\"no-apply-heal-state\"}"; }

	// A faithful snapshot of THIS body: the same capture the 1 Hz report and the save cut use.
	var message = captureMethod.Invoke(sync, new object[] { body });
	if (message == null) { return "{\"ok\":false,\"error\":\"no-captured-message\"}"; }
	var limbsProperty = message.GetType().GetProperty("Limbs", flags);
	if (limbsProperty == null) { return "{\"ok\":false,\"error\":\"no-limbs-property\"}"; }
	var limbs = limbsProperty.GetValue(message, null) as System.Collections.IEnumerable;
	if (limbs == null) { return "{\"ok\":false,\"error\":\"no-limbs\"}"; }

	System.Object row = null;
	foreach (var candidate in limbs) {
		var indexProperty = candidate.GetType().GetProperty("Index", flags);
		var candidateIndex = indexProperty != null ? (int)indexProperty.GetValue(candidate, null) : -1;
		if (candidateIndex == limbIndex) { row = candidate; break; }
	}
	if (row == null) { return "{\"ok\":false,\"error\":\"no-such-limb-row\",\"detail\":\"limb " + limbIndex + "\"}"; }

	var componentsProperty = row.GetType().GetProperty("Components", flags);
	if (componentsProperty == null) { return "{\"ok\":false,\"error\":\"no-components-property\"}"; }
	var rowComponents = componentsProperty.GetValue(row, null) as System.Collections.IList;
	if (rowComponents == null) { return "{\"ok\":false,\"error\":\"no-components-list\"}"; }

	if (limb.GetComponent<SplintLimb>() != null) { componentsBefore = "SplintLimb"; }
	if (limb.GetComponent<TourniquetScript>() != null) { componentsBefore = componentsBefore.Length > 0 ? componentsBefore + ",TourniquetScript" : "TourniquetScript"; }
	if (limb.GetComponent<ChilledLimb>() != null) { componentsBefore = componentsBefore.Length > 0 ? componentsBefore + ",ChilledLimb" : "ChilledLimb"; }
	var splintBefore = limb.GetComponent<SplintLimb>();
	if (splintBefore != null) {
		conditionBefore = splintBefore.condition;
		try { startRanBefore = startField != null && startField.GetValue(splintBefore) != null; } catch { startRanBefore = false; }
	}
	splintedBefore = limb.splinted;

	// The authority's own shape for a removal: the limb's component list is complete and now EMPTY, and
	// the flag it cleared rides the same message (`OtherMedicalRemovalApplier.Remove`). The count below
	// is read AFTER the clear, so it is the list the receiving path is actually handed.
	rowComponents.Clear();
	messageComponents = rowComponents.Count;
	var splintedProperty = row.GetType().GetProperty("Splinted", flags);
	if (splintedProperty != null) { splintedProperty.SetValue(row, false, null); }
	var healthProperty = message.GetType().GetProperty("Health", flags);
	var health = healthProperty != null ? healthProperty.GetValue(message, null) : null;

	// The production call itself, exactly as `MedicalOperationApply.ApplyProgress` makes it for a
	// target-local client.
	applyMethod.Invoke(sync, new object[] { body, health, limbsProperty.GetValue(message, null) });

	if (limb.GetComponent<SplintLimb>() != null) { componentsAfter = "SplintLimb"; }
	if (limb.GetComponent<TourniquetScript>() != null) { componentsAfter = componentsAfter.Length > 0 ? componentsAfter + ",TourniquetScript" : "TourniquetScript"; }
	if (limb.GetComponent<ChilledLimb>() != null) { componentsAfter = componentsAfter.Length > 0 ? componentsAfter + ",ChilledLimb" : "ChilledLimb"; }
	var splintAfter = limb.GetComponent<SplintLimb>();
	if (splintAfter != null) {
		conditionAfter = splintAfter.condition;
		try { startRanAfter = startField != null && startField.GetValue(splintAfter) != null; } catch { startRanAfter = false; }
	}
	splintedAfter = limb.splinted;

	return "{\"ok\":true,\"mode\":\"message-clear\",\"limb\":" + limbIndex
		+ ",\"componentsBefore\":\"" + componentsBefore + "\",\"componentsAfter\":\"" + componentsAfter + "\""
		+ ",\"splintedBefore\":" + (splintedBefore ? "true" : "false") + ",\"splintedAfter\":" + (splintedAfter ? "true" : "false")
		+ ",\"startRanBefore\":" + (startRanBefore ? "true" : "false") + ",\"startRanAfter\":" + (startRanAfter ? "true" : "false")
		+ ",\"conditionBefore\":" + num(conditionBefore) + ",\"conditionAfter\":" + num(conditionAfter)
		+ ",\"enabled\":\"n/a\",\"messageComponents\":" + messageComponents + "}";
}))()
