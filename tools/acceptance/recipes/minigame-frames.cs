// recipe: minigame-frames
// args: none
// returns: ok, frames, count, error
//
// The minigame's own hand state frame by frame, as the acceptance assembly recorded it (a postfix on `MinigameBase.Update`).
// A single reading cannot say WHEN the hand stopped: the bandage row held the attack for 34 s and read `handStarted: false` at
// the end, and only the history says whether the start edge fired once and was cleared, never fired, or fired and the hold
// dropped on the next frame (measured 2026-11-26, `docs/backlog/todo/1.0-the-bandage-hand-never-holds-the-wrap.md`).
((System.Func<string>)(() => {
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length; i++) {
		System.Type recorder; try { recorder = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.Acceptance.MinigameFrameRecorder", false); } catch { recorder = null; }
		if (recorder == null) { continue; }
		var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
		var read = recorder.GetMethod("Read", flags);
		if (read == null) { return "{\"ok\":false,\"error\":\"no Read method\"}"; }
		var frames = read.Invoke(null, null) as string ?? "";
		var count = frames.Length == 0 ? 0 : frames.Split('\n').Length;
		var escaped = frames.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
		return "{\"ok\":true,\"count\":" + count.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"frames\":\"" + escaped + "\"}";
	}
	return "{\"ok\":false,\"error\":\"the acceptance assembly is not loaded on this side\"}";
}))()
