// recipe: frame-clock
// args: none
// returns: ok, frame, realtime, unscaled, timeScale, runInBackground
// serves: acceptance-row-review-campaign
//
// Two reads a second apart say whether a client is really running: a window that lost focus, or a build that stops
// updating in the background, advances its frame count at a fraction of real time and every held-input row then measures
// nothing (2026-11-22: three `PlayerCamera.HandleAttacks` entries across a fifteen-second attack hold).
((System.Func<string>)(() => {
    return "{\"ok\":true,\"frame\":" + UnityEngine.Time.frameCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + ",\"realtime\":" + UnityEngine.Time.realtimeSinceStartup.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
        + ",\"unscaled\":" + UnityEngine.Time.unscaledTime.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
        + ",\"timeScale\":" + UnityEngine.Time.timeScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
        + ",\"runInBackground\":" + (UnityEngine.Application.runInBackground ? "true" : "false") + "}";
}))()
