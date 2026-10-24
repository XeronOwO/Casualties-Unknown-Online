// recipe: remote-hand
// args: none
// serves: remote-medical-panel-hide-local-only-actions row 6 (a hand switch on either side of a blocked window still reaches the peer)
// returns: ok, localHand, clones (name:handSlot:itemId, comma separated), count, error, detail
//
// Reads every Body in the scene that is NOT this client's own body - the remote clones a peer's hand switch has to move - with
// the hand slot and the id of the item in it. The ticket's row 6 is about a hand switch BEFORE and AFTER a remote focus still
// reaching the peer, so the reading that answers it is the peer's own clone, not the local body's field.
((System.Func<string>)(() => {
	var local = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (local == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var bodies = UnityEngine.Object.FindObjectsOfType<Body>();
	var parts = new System.Collections.Generic.List<string>();
	var count = 0;
	for (var i = 0; i < bodies.Length; i++) {
		var candidate = bodies[i];
		if (candidate == null || candidate == local) { continue; }
		if (!candidate.gameObject.activeInHierarchy) { continue; }
		var held = candidate.GetItem(candidate.handSlot);
		var itemId = held == null ? "none" : held.id;
		var slotZero = candidate.GetItem(0);
		var slotOne = candidate.GetItem(1);
		var zeroId = slotZero == null ? "none" : slotZero.id;
		var oneId = slotOne == null ? "none" : slotOne.id;
		count++;
		parts.Add(candidate.gameObject.name + ":" + candidate.handSlot.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + itemId + ":s0=" + zeroId + ":s1=" + oneId);
	}
	return "{\"ok\":true,\"localHand\":" + local.handSlot.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"count\":" + count.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"clones\":\"" + string.Join("|", parts) + "\"}";
}))()
