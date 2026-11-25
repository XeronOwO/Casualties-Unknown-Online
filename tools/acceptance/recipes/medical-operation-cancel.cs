// recipe: medical-operation-cancel
// args: none
// serves: every medical row that starts an operation, and the row after it
// returns: ok, cancelled, service, control, injection, shrapnel, other, error
//
// Ends every medical operation THIS client still holds, through the product's own session reset — the same four
// tables and the same claim set the authority clears when a session ends (`MedicalOperationSessionService
// .ResetSessionState`). A row that starts a compression or an injection and stops on its assertion leaves the
// operation open on the authority, and the next medical row's start is refused by name: "[Shrapnel] refused start
// for 1 on <target>: Operator already has an active medical operation." (measured 2026-11-25: medical-cpr then
// medical-shrapnel in one batch; the CPR operation stayed open on the host until its own 20 s idle timeout, the
// shrapnel session never started, and the operator's hand had nothing to grab while the minigame opened locally).
//
// The three family tables are counted before the reset so the note can name them: `_active` holds the injection
// sessions, `_shrapnel._sessions` the shared shrapnel ones, `_other._sessions` the Stage 3 ones (bandage, removal,
// dislocation, defib, amputation). The refusal is `claims.IsOperatorBusy`, which ResetSessionState clears too.
// `cancelled` is empty when nothing was open, so a caller can stay quiet on a clean pair.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":true,\"cancelled\":\"\",\"service\":false}"; }
	var control = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.IMedicalOperationControl));
	if (control == null) { return "{\"ok\":true,\"cancelled\":\"\",\"service\":true,\"control\":false}"; }
	const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var type = control.GetType();

	int Count(string field)
	{
		var member = type.GetField(field, Flags);
		if (member == null) { return 0; }
		var table = member.GetValue(control);
		if (table == null) { return 0; }
		var sessions = table.GetType().GetField("_sessions", Flags);
		var value = sessions == null ? table : sessions.GetValue(table);
		if (value is System.Collections.IDictionary dictionary) { return dictionary.Count; }
		return 0;
	}

	var injection = Count("_active");
	var shrapnel = Count("_shrapnel");
	var other = Count("_other");
	var reset = type.GetMethod("ResetSessionState", Flags);
	if (reset == null) { return "{\"ok\":false,\"error\":\"no-reset\",\"injection\":" + injection.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}"; }

	var open = injection + shrapnel + other;
	if (open == 0) { return "{\"ok\":true,\"cancelled\":\"\",\"injection\":0,\"shrapnel\":0,\"other\":0}"; }

	reset.Invoke(control, null);
	var named = new System.Collections.Generic.List<string>();
	if (injection > 0) { named.Add(injection.ToString(System.Globalization.CultureInfo.InvariantCulture) + " injection"); }
	if (shrapnel > 0) { named.Add(shrapnel.ToString(System.Globalization.CultureInfo.InvariantCulture) + " shrapnel"); }
	if (other > 0) { named.Add(other.ToString(System.Globalization.CultureInfo.InvariantCulture) + " other"); }
	return "{\"ok\":true,\"injection\":" + injection.ToString(System.Globalization.CultureInfo.InvariantCulture) +
		",\"shrapnel\":" + shrapnel.ToString(System.Globalization.CultureInfo.InvariantCulture) +
		",\"other\":" + other.ToString(System.Globalization.CultureInfo.InvariantCulture) +
		",\"cancelled\":\"" + string.Join(" + ", named) + "\"}";
}))()
