// recipe: sound-log
// args: mode=s match=s
// returns: ok, line, error, detail
//
// Drives the one-shot recorder: `mode=start` clears and arms it, `mode=read` answers `raw=<calls seen>|clip@time;…`,
// `mode=stop` disarms it. `raw` counts every `Sound.Play` the prefixes have seen since the process started, armed or not, so
// a zero means the patch never ran while a non-zero with no entries means it ran and nothing was staged. The formatting
// lives in the recorder, not here - a reading must not depend on reflection over the entry struct.
//
// ONE LINE ON PURPOSE: the client's evaluator takes one top-level expression.
((System.Func<string>)(() => { var mode = {{s:mode}}; var match = {{s:match}}; var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic; var assemblies = System.AppDomain.CurrentDomain.GetAssemblies(); var recorder = (System.Type)null; for (var i = 0; i < assemblies.Length; i++) { try { recorder = assemblies[i].GetType("CasualtiesUnknownOnline.Acceptance.SoundPlayRecorder", false); } catch { recorder = null; } if (recorder != null) { break; } } if (recorder == null) { return "{\"ok\":false,\"error\":\"recorder-not-loaded\"}"; } if (mode == "start") { recorder.GetMethod("Start", flags).Invoke(null, null); } else if (mode == "stop") { recorder.GetMethod("Stop", flags).Invoke(null, null); } var read = recorder.GetMethod("Read", flags).Invoke(null, new object[] { match }) as string; return "{\"ok\":true,\"line\":\"" + (read ?? "") + "\"}"; }))()