// recipe: generation-audio-probe
// args: mode=s
//        mode is `read` (the only mode today). Each token on the args line must be <key>=s or <key>=n.
// serves: done/layer-descent-audio-parity.md - the descent whoosh is played by whichever client runs its own
//         generation, under `biomeDepth == 0 && biomeOverride == None`, so the question a run has to answer is
//         whether a MEMBER reaches that branch at all. Sampling during the member's own load is the reading.
// returns: ok, mode, generatingWorld, instantiatingWorld, biomeDepth, biomeOverride, genTimePassed,
//          podPlaying, podTime (the source's own time - non-zero means it really played), caveAudioPlaying.
//
// The fields are the game's own, read off the live WorldGeneration instance rather than inferred from the log:
// `genTimePassed` only accumulates INSIDE the first-layer branch, and the AudioSource's own `time` is the one
// value that says the clip actually started rather than that the code reached the line.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	if (mode != "read") { return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be read\"}"; }
	var world = WorldGeneration.world;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
	var culture = System.Globalization.CultureInfo.InvariantCulture;
	var pod = world.genPodSource;
	var podPlaying = pod != null && pod.isPlaying;
	var podTime = pod != null ? pod.time : 0f;
	var cave = world.caveAudio;
	var cavePlaying = cave != null && cave.isPlaying;
	return "{\"ok\":true,\"mode\":\"read\""
		+ ",\"generatingWorld\":" + (world.generatingWorld ? "true" : "false")
		+ ",\"instantiatingWorld\":" + (world.instantiatingWorld ? "true" : "false")
		+ ",\"biomeDepth\":" + world.biomeDepth.ToString(culture)
		+ ",\"biomeOverride\":\"" + world.biomeOverride.ToString() + "\""
		+ ",\"genTimePassed\":" + world.genTimePassed.ToString("0.###", culture)
		+ ",\"podPlaying\":" + (podPlaying ? "true" : "false")
		+ ",\"podTime\":" + podTime.ToString("0.###", culture)
		+ ",\"caveAudioPlaying\":" + (cavePlaying ? "true" : "false") + "}";
}))()
