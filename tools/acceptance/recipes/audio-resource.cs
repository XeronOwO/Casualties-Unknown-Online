// recipe: audio-resource
// args: clip=s
// returns: ok, clip, present, error, detail
//
// Answers whether this build actually carries a clip under `Resources/Sounds/<clip>`, which is the path `Sound.Play(string clip, …)` loads from
// (reversing/Assembly-CSharp/Assembly-CSharp/Sound.cs: `Resources.Load<AudioClip>("Sounds/" + clip)`). It exists because a sound row can be blocked by a clip the
// build does not have rather than by the relay: a `Sound.Play` whose resource is missing returns without playing anything, on every client, and no listener-side
// reading can tell that apart from "the event never happened".
((System.Func<string>)(() => {
	var clip = {{s:clip}};
	var loaded = Resources.Load<AudioClip>("Sounds/" + clip);
	return "{\"ok\":true,\"clip\":\"" + clip + "\",\"present\":" + (loaded != null ? "true" : "false") + "}";
}))()
