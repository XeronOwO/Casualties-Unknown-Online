// recipe: selfharm-finish
// args: none
// serves: operational recovery - a body whose happiness hit zero starts a native `SelfHarmMinigame` that cannot be exited
//         (`CanExit()` returns false by design) and blocks the drag bracket while it runs; this finishes it the way the
//         player does, through the game's own `StartCut`/`EndCut` pair
// returns: ok, before, cutsLeft, cuts, after, error, detail
//
// `SelfHarmMinigame.StartCut` decrements `cutsLeft` and `EndCut` calls `EndMinigame()` once it reaches zero, so making the
// remaining cuts is the only exit the game offers. The loop is bounded: a minigame that does not end within its own cut count
// is reported, never looped on.
((System.Func<string>)(() => {
	var baseMain = MinigameBase.main;
	if (baseMain == null) { return "{\"ok\":false,\"error\":\"no-minigame-base\"}"; }
	var before = baseMain.currentMinigame == null ? "none" : baseMain.currentMinigame.GetType().Name;
	var game = baseMain.currentMinigame as SelfHarmMinigame;
	if (game == null) { return "{\"ok\":true,\"before\":\"" + before + "\",\"cutsLeft\":-1,\"cuts\":0,\"after\":\"" + before + "\"}"; }
	var cutsField = typeof(SelfHarmMinigame).GetField("cutsLeft", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
	var cutsLeft = cutsField == null ? -1 : (int)cutsField.GetValue(game);
	var cuts = 0;
	for (var i = 0; i < 20 && baseMain.currentMinigame == game; i++) {
		game.StartCut();
		game.EndCut();
		cuts++;
	}
	var after = baseMain.currentMinigame == null ? "none" : baseMain.currentMinigame.GetType().Name;
	return "{\"ok\":true,\"before\":\"" + before + "\""
		+ ",\"cutsLeft\":" + cutsLeft.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"cuts\":" + cuts.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"after\":\"" + after + "\"}";
}))()
