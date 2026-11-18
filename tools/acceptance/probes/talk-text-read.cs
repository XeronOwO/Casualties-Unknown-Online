// recipe: talk-text-read
// args: none
// returns: ok, hasText, enabled, text, colorAlpha, medicalOpen, error, detail
//
// Reads the native on-screen speech line the player sees (`PlayerCamera.charTalkText`) and whether the remote medical view is open at
// the same moment. It serves the medical-panel speech ticket's trade-menu row: the takeover and restore rules are about what this line
// holds while a remote panel is open over the trade menu, and what it holds after the panel closes — the peer's last line must never be
// there. Read-only; it never writes the line.
((System.Func<string>)(() => {
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	// The member is walked up the INHERITANCE CHAIN: `Type.GetField`/`GetProperty` answer only what the declaring type exposes plus
	// inherited public/protected members, so a private member declared on a base class reads as absent — and a reflected member read that
	// answers its default is a defect in the READ (the tools rules say exactly this).
	System.Reflection.PropertyInfo property = null;
	System.Reflection.FieldInfo field = null;
	for (var type = typeof(PlayerCamera); type != null && property == null && field == null; type = type.BaseType) {
		property = type.GetProperty("charTalkText", flags | System.Reflection.BindingFlags.DeclaredOnly);
		if (property == null) { field = type.GetField("charTalkText", flags | System.Reflection.BindingFlags.DeclaredOnly); }
	}
	object raw = null;
	if (property != null) { raw = property.GetValue(camera, null); } else if (field != null) { raw = field.GetValue(camera); }
	// The game declares it as `public TextMeshProUGUI charTalkText` (`reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs`), so the
	// cast is to TextMeshPro's component; casting to UnityEngine.UI.Text answered null and read as "no text" on a member that exists.
	var text = raw as TMPro.TextMeshProUGUI;
	if (text == null) { return "{\"ok\":true,\"hasText\":false,\"detail\":\"" + (raw == null ? "null" : raw.GetType().Name) + "\"}"; }

	var enabled = text.enabled;
	var current = text.text ?? "";
	var alpha = text.color.a;

	System.Type view = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length; i++) {
		try { view = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.RemoteMedicalView", false); } catch { view = null; }
		if (view != null) { break; }
	}
	var open = false;
	if (view != null) {
		var openProperty = view.GetProperty("IsOpen", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		if (openProperty != null) { open = (bool)openProperty.GetValue(null, null); }
	}

	return "{\"ok\":true,\"hasText\":true,\"enabled\":" + (enabled ? "true" : "false")
		+ ",\"text\":\"" + current.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
		+ ",\"colorAlpha\":" + alpha.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"medicalOpen\":" + (open ? "true" : "false") + "}";
}))()
