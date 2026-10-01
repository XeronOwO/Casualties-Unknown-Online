// recipe: item-floating
// args: value=n
// serves: entity-destruction-drop-guest-fresh-state-loss, trap-destruction-drop-quantity-desync
// returns: ok, target, settingFound, settingBefore, settingAfter, staticBefore, staticAfterApply,
//          applyProducedTarget, staticEnforced, armed, changed
//
// Reads and arms the game's own fresh-drop setting: the `itemfloating` SettingBool whose apply writes
// Item.itemFloating - the static FreshItemDrop.Awake reads before it destroys itself, so with the
// effect off no drop ever carries the fresh presentation and those rows cannot be judged. The answer
// carries the static's BEFORE and the value the game's own apply produced (staticAfterApply), because
// the setting's default is true (Settings.cs:208) and LoadSettings -> ApplyAll already wrote the static
// at startup: `value=1` on an untouched machine legitimately moves nothing, and a run must be able to
// tell "already armed" from "armed now". The recipe then enforces the static itself
// (staticEnforced=true) for the case where the setting is unreachable; `armed` is the state the drops
// will actually see. One eval.
((System.Func<string>)(() => {
	var target = (int)({{n:value}}) != 0;
	var staticBefore = Item.itemFloating;
	var settingFound = false;
	var settingBefore = "unknown";
	var settingAfter = "unknown";
	var staticAfterApply = staticBefore;
	try {
		var setting = Settings.Get<SettingBool>("itemfloating");
		if (setting != null) {
			settingFound = true;
			settingBefore = setting.value ? "true" : "false";
			setting.value = target;
			setting.Apply();
			settingAfter = setting.value ? "true" : "false";
			staticAfterApply = Item.itemFloating;
		}
	} catch (System.Exception) {
		settingFound = false;
	}
	Item.itemFloating = target;
	return "{\"ok\":true,\"target\":" + (target ? "true" : "false")
		+ ",\"settingFound\":" + (settingFound ? "true" : "false")
		+ ",\"settingBefore\":\"" + settingBefore + "\""
		+ ",\"settingAfter\":\"" + settingAfter + "\""
		+ ",\"staticBefore\":" + (staticBefore ? "true" : "false")
		+ ",\"staticAfterApply\":" + (staticAfterApply ? "true" : "false")
		+ ",\"applyProducedTarget\":" + (staticAfterApply == target ? "true" : "false")
		+ ",\"staticEnforced\":true"
		+ ",\"armed\":" + (Item.itemFloating == target ? "true" : "false")
		+ ",\"changed\":" + (Item.itemFloating != staticBefore ? "true" : "false") + "}";
}))()
