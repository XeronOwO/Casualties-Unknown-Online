// recipe: sound-play-probe
// args: none
// returns: ok, rawCalls, error, detail
//
// Asks the recorder to play one probe sound through the game's OWN `Sound.Play` entry, from a compiled call site inside the
// batch-only assembly, and answers how many calls its prefixes have seen. A managed call compiled there is a fresh call
// site, so a zero answer means the patch does not reach the method at all - which is a different fault from "the buffer was
// empty because nothing was armed".
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => { var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic; var recorder = (System.Type)null; var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); for (var i = 0; i < assemblies.Length; i++) { try { recorder = assemblies[i].GetType("CasualtiesUnknownOnline.Acceptance.SoundPlayRecorder", false); } catch { recorder = null; } if (recorder != null) { break; } } if (recorder == null) { return "{\"ok\":false,\"error\":\"recorder-not-loaded\"}"; } var probe = recorder.GetMethod("ProbePlay", flags); if (probe == null) { return "{\"ok\":false,\"error\":\"no-probe\"}"; } var raw = System.Convert.ToInt64(probe.Invoke(null, null), System.Globalization.CultureInfo.InvariantCulture); return "{\"ok\":true,\"rawCalls\":" + raw.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}"; }))()