// recipe: frame-clock
// args: none
// returns: ok, frame, realtime, unscaled, timeScale, runInBackground, paused, console, forceNoMovement
// serves: acceptance-row-review-campaign
//
// Two reads a second apart say whether a client is really running: a window that lost focus, or a build that stops
// updating in the background, advances its frame count at a fraction of real time and every held-input row then measures
// nothing (2026-11-22: three `PlayerCamera.HandleAttacks` entries across a fifteen-second attack hold). The last three
// fields are the game's own input gates — `PlayerCamera.HandleInput` returns early on the console, on
// `PauseHandler.paused`, and on a crafting or trade panel (`PlayerCamera.cs:981`, `:1023`, `:1039`).
((System.Func<string>)(() => {
    var paused = false;
    try { paused = PauseHandler.paused; } catch { paused = false; }
    var console = false;
    try { console = ConsoleScript.instance != null && ConsoleScript.instance.active; } catch { console = false; }
    var forced = false;
    try { forced = PlayerCamera.main != null && PlayerCamera.main.body != null && PlayerCamera.main.body.forceNoMovement; } catch { forced = false; }
    return "{\"ok\":true,\"frame\":" + UnityEngine.Time.frameCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + ",\"realtime\":" + UnityEngine.Time.realtimeSinceStartup.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
        + ",\"unscaled\":" + UnityEngine.Time.unscaledTime.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
        + ",\"timeScale\":" + UnityEngine.Time.timeScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
        + ",\"runInBackground\":" + (UnityEngine.Application.runInBackground ? "true" : "false")
        + ",\"paused\":" + (paused ? "true" : "false")
        + ",\"console\":" + (console ? "true" : "false")
        + ",\"forceNoMovement\":" + (forced ? "true" : "false") + "}";
}))()
