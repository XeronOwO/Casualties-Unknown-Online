// recipe: lockpick-target
// args: none
// returns: ok, kind, targetName, targetHealth, targetNull, handStarted, clickingInside, progress, max
//
// What the running lockpick minigame is trying to destroy, read from the game's own object. `LockpingMinigame.Update`
// ends the minigame the moment `toDestroy` is falsy (`LockpingMinigame.cs:108-110`), and a crate that was moved or
// unloaded after the click is exactly that case — measured 2026-11-23: `lock-25` read `active:true` and one round
// trip later the minigame was gone, which a null target explains without any timing story.
((System.Func<string>)(() => {
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var main = MinigameBase.main;
	if (main == null) { return "{\"ok\":false,\"error\":\"no-minigame-base\"}"; }
	var current = main.currentMinigame;
	if (current == null) { return "{\"ok\":false,\"error\":\"no-minigame\"}"; }
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var type = current.GetType();
	var targetField = type.GetField("toDestroy", flags);
	var target = targetField?.GetValue(current);
	var handStarted = type.GetField("handStartedClicking", flags)?.GetValue(current);
	var inside = type.GetField("clickingInside", flags)?.GetValue(current);
	var progress = System.Convert.ToSingle(type.GetField("lockProgress", flags)?.GetValue(current) ?? 0f);
	var maxMethod = type.GetMethod("MaxTurnProgress", flags);
	var max = maxMethod == null ? 0f : System.Convert.ToSingle(maxMethod.Invoke(current, null));

	var name = "";
	var health = -1f;
	if (target != null) {
		var behaviour = target as UnityEngine.Behaviour;
		if (behaviour != null) { name = behaviour.name; }
		var healthProp = target.GetType().GetProperty("health", flags);
		var healthField = healthProp == null ? target.GetType().GetField("health", flags) : null;
		var raw = healthProp != null ? healthProp.GetValue(target) : (healthField != null ? healthField.GetValue(target) : null);
		if (raw != null) { health = System.Convert.ToSingle(raw); }
	}

	return "{\"ok\":true,\"kind\":\"" + type.Name + "\""
		+ ",\"targetNull\":" + (target == null ? "true" : "false")
		+ ",\"targetName\":\"" + name.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
		+ ",\"targetHealth\":" + health.ToString("0.###", inv)
		+ ",\"handStarted\":" + (handStarted is bool h && h ? "true" : "false")
		+ ",\"clickingInside\":" + (inside is bool i && i ? "true" : "false")
		+ ",\"progress\":" + progress.ToString("0.###", inv)
		+ ",\"max\":" + max.ToString("0.###", inv) + "}";
}))()
