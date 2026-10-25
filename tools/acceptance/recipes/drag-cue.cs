// recipe: drag-cue
// args: none
// returns: ok, showing, targetSteamId, displayName, text, error, detail
//
// Reads the CUO drag-use cue the way the overlay draws it: `IDragUseCueState.Showing` and `TargetSteamId` from the running
// services, then the same two facts the overlay composes into its line — the target's display name and the localized
// `cue.use_on` text. This is the reading `cross-player-drag-use-feedback` row 1 asks for: a cue that NAMES the teammate the
// release would use the item on, and disappears when the cursor leaves.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var cueType = (System.Type)null;
	var uiType = (System.Type)null;
	var locType = (System.Type)null;
	var asms = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < asms.Length; i++) {
		if (cueType == null) { try { cueType = asms[i].GetType("CasualtiesUnknownOnline.Runtime.GameAdapter.IDragUseCueState", false); } catch { cueType = null; } }
		if (uiType == null) { try { uiType = asms[i].GetType("CasualtiesUnknownOnline.Runtime.Session.OnlineUiState", false); } catch { uiType = null; } }
		if (locType == null) { try { locType = asms[i].GetType("CasualtiesUnknownOnline.Runtime.Localization.LocalizationService", false); } catch { locType = null; } }
	}
	if (cueType == null) { return "{\"ok\":false,\"error\":\"no-cue-state-type\"}"; }
	var cue = services.GetService(cueType);
	if (cue == null) { return "{\"ok\":false,\"error\":\"no-cue-state-service\"}"; }
	var showing = (bool)cueType.GetProperty("Showing").GetValue(cue, null);
	var target = (ulong)cueType.GetProperty("TargetSteamId").GetValue(cue, null);
	var name = "";
	if (uiType != null) {
		var ui = services.GetService(uiType);
		if (ui != null) {
			var m = uiType.GetMethod("DisplayName") ?? uiType.GetMethod("NameOf");
			if (m != null) {
				try {
					var args = m.GetParameters().Length == 1 ? new object[] { target } : new object[] { target.ToString() };
					var answer = m.Invoke(ui, args);
					name = answer == null ? "" : answer.ToString();
				} catch { name = ""; }
			}
		}
	}
	var text = "";
	if (locType != null) {
		var loc = services.GetService(locType);
		if (loc != null) {
			var fmt = locType.GetMethod("Format", new System.Type[] { typeof(string), typeof(object[]) });
			if (fmt != null) {
				try { text = (string)fmt.Invoke(loc, new object[] { "cue.use_on", new object[] { name.Length == 0 ? target.ToString() : name } }); } catch { text = ""; }
			}
		}
	}
	return "{\"ok\":true,\"showing\":" + (showing ? "true" : "false")
		+ ",\"targetSteamId\":\"" + target.ToString() + "\""
		+ ",\"displayName\":\"" + name.Replace("\\", "/").Replace("\"", "'") + "\""
		+ ",\"text\":\"" + (text ?? "").Replace("\\", "/").Replace("\"", "'") + "\"}";
}))()
