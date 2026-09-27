// recipe: carry-read
// args: none
// serves: carry-piggyback-rider-position-smoothing, carry-piggyback-vertical-placement-asymmetry,
//         carrier-sit-while-carrying, carried-player-idle-sit-suppression, carried-unconscious-body-simulation,
//         carried-inventory-registration-re-report, guest-container-contents-ghost-drops-on-host
// returns: ok, local, role, inWorld, localCarries, localCarriedBy, localDriverCarrier, localBody,
//          clones (one entry per remote Body clone: the carry-pin readings, the two log-line tokens and
//          the pose inputs)
//
// Reads the facts the carry family is judged on from inside one client: the host-owned relation mirror,
// the carried-body driver on the local body, the local body's own simulation fields, and — for every
// remote clone — the 1 Hz diagnostic window's readings (pinCount, riderDrift, limbSeparation, who the
// pin was anchored on) plus the two facts that line names as tokens: `pinned-to-carrier` (a pin was in
// force inside the window) and `mounted-to-local-carrier` (the clone root hangs under the local
// carrier's CUO_CarryMount). Zero included, so "measured zero" and "not measured" stay distinguishable.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var findType = new System.Func<string, System.Type>((fullName) => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var index = 0; index < assemblies.Length; index++) {
			System.Type candidate = null;
			try { candidate = assemblies[index].GetType(fullName, false); } catch { candidate = null; }
			if (candidate != null) { return candidate; }
		}
		return null;
	});
	var getBool = new System.Func<object, string, bool>((target, name) => {
		var field = target.GetType().GetField(name, instanceFlags);
		return field == null ? false : System.Convert.ToBoolean(field.GetValue(target));
	});
	var getFloat = new System.Func<object, string, float>((target, name) => {
		var field = target.GetType().GetField(name, instanceFlags);
		return field == null ? 0f : System.Convert.ToSingle(field.GetValue(target));
	});
	var getInt = new System.Func<object, string, int>((target, name) => {
		var field = target.GetType().GetField(name, instanceFlags);
		return field == null ? 0 : System.Convert.ToInt32(field.GetValue(target));
	});
	var getUlong = new System.Func<object, string, ulong>((target, name) => {
		var field = target.GetType().GetField(name, instanceFlags);
		return field == null ? 0UL : System.Convert.ToUInt64(field.GetValue(target));
	});
	var num = new System.Func<float, string>((value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.SessionService)) as CasualtiesUnknownOnline.Runtime.Session.SessionService;
	var control = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.IPlayerInteractionControl)) as CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.IPlayerInteractionControl;
	if (session == null || control == null) { return "{\"ok\":false,\"error\":\"no-player-interaction\"}"; }
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var carriedDriverType = findType("CasualtiesUnknownOnline.GameAdapter.Character.CarriedBodyDriver");
	var remoteDriverType = findType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteBodyDriver");
	var localDriverCarrier = 0UL;
	if (carriedDriverType != null) {
		var localDriver = body.GetComponent(carriedDriverType);
		if (localDriver != null) { localDriverCarrier = getUlong(localDriver, "CarrierSteamId"); }
	}
	var localCarried = 0UL;
	var localCarrier = 0UL;
	var carries = control.TryGetCarried(session.LocalSteamId, out localCarried);
	var carriedBy = control.TryGetCarrier(session.LocalSteamId, out localCarrier);
	var clip = "";
	var clips = body.bodyAnimator.GetCurrentAnimatorClipInfo(0);
	if (clips != null && clips.Length > 0) { clip = clips[0].clip.name; }
	var sb = new System.Text.StringBuilder();
	sb.Append("{\"ok\":true");
	sb.Append(",\"local\":\"").Append(session.LocalSteamId).Append("\"");
	sb.Append(",\"role\":\"").Append(session.Role).Append("\"");
	sb.Append(",\"inWorld\":").Append(session.LocalInWorld ? "true" : "false");
	sb.Append(",\"localCarries\":\"").Append(carries ? localCarried.ToString() : "0").Append("\"");
	sb.Append(",\"localCarriedBy\":\"").Append(carriedBy ? localCarrier.ToString() : "0").Append("\"");
	sb.Append(",\"localDriverCarrier\":\"").Append(localDriverCarrier.ToString()).Append("\"");
	sb.Append(",\"localBody\":{");
	sb.Append("\"standing\":").Append(body.standing ? "true" : "false");
	sb.Append(",\"sleeping\":").Append(body.sleeping ? "true" : "false");
	sb.Append(",\"crouching\":").Append(body.crouching ? "true" : "false");
	sb.Append(",\"alive\":").Append(body.alive ? "true" : "false");
	sb.Append(",\"conscious\":").Append(body.conscious ? "true" : "false");
	sb.Append(",\"consciousness\":").Append(num(body.consciousness));
	sb.Append(",\"brainHealth\":").Append(num(body.brainHealth));
	sb.Append(",\"energy\":").Append(num(body.energy));
	sb.Append(",\"badSleepAmount\":").Append(num(body.badSleepAmount));
	sb.Append(",\"idleTime\":").Append(num(body.idleTime));
	sb.Append(",\"moveDirX\":").Append(num(body.moveDir.x));
	sb.Append(",\"moveDirY\":").Append(num(body.moveDir.y));
	sb.Append(",\"velocityX\":").Append(num(body.rb.velocity.x));
	sb.Append(",\"velocityY\":").Append(num(body.rb.velocity.y));
	sb.Append(",\"legSpeedMult\":").Append(num(body.legSpeedMult));
	sb.Append(",\"positionX\":").Append(num(body.transform.position.x));
	sb.Append(",\"positionY\":").Append(num(body.transform.position.y));
	sb.Append(",\"animatorClip\":\"").Append(clip).Append("\"}");
	sb.Append(",\"clones\":[");
	var found = UnityEngine.Object.FindObjectsOfType(typeof(Body));
	var first = true;
	for (var i = 0; i < found.Length; i++) {
		var clone = found[i] as Body;
		if (clone == null || clone == body) { continue; }
		var driver = remoteDriverType == null ? null : clone.GetComponent(remoteDriverType);
		if (driver == null) { continue; }
		if (!first) { sb.Append(","); }
		first = false;
		sb.Append("{\"limbSeparationMax\":").Append(num(getFloat(driver, "LimbSeparationWindowMax")));
		sb.Append(",\"riderDriftMax\":").Append(num(getFloat(driver, "PinDriftWindowMax")));
		sb.Append(",\"pinCount\":").Append(getInt(driver, "PinCountInWindow").ToString(System.Globalization.CultureInfo.InvariantCulture));
		sb.Append(",\"pinnedToCarrier\":").Append(getInt(driver, "PinCountInWindow") > 0 ? "true" : "false");
		sb.Append(",\"pinnedCarrier\":\"").Append(getUlong(driver, "PinnedCarrierSteamId").ToString()).Append("\"");
		sb.Append(",\"pinnedToLocalCarrier\":").Append(getBool(driver, "PinnedToLocalCarrier") ? "true" : "false");
		sb.Append(",\"isCarriedRider\":").Append(getBool(driver, "IsCarriedRider") ? "true" : "false");
		sb.Append(",\"isCarrier\":").Append(getBool(driver, "IsCarrier") ? "true" : "false");
		sb.Append(",\"ragdollPoseActive\":").Append(getBool(driver, "RagdollPoseActive") ? "true" : "false");
		sb.Append(",\"legSpeedMult\":").Append(num(getFloat(driver, "LegSpeedMult")));
		sb.Append(",\"mountedToLocalCarrier\":").Append(clone.transform.parent != null
			&& clone.transform.parent.parent != null
			&& clone.transform.parent.parent.name == "CUO_CarryMount" ? "true" : "false");
		sb.Append(",\"positionX\":").Append(num(clone.transform.position.x));
		sb.Append(",\"positionY\":").Append(num(clone.transform.position.y)).Append("}");
	}
	sb.Append("]}");
	return sb.ToString();
}))()
