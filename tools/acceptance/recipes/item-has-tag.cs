// recipe: item-has-tag
// args: id=s
// serves: mod-item-with-no-declared-tags-throws rows 1 and 2 (a definition that declares no tags
//         still hands the game a present derived tag array, and the game's own `ItemInfo.HasTag`
//         answers for it instead of throwing)
// returns: ok, id, present, arrayPresent, arrayLength, tags, probeTag, hasCarried, hasAbsent, error, detail
//          hasCarried and hasAbsent are three-state STRINGS ("true", "false", "threw:<ExceptionType>"), not
//          booleans: the probe must be able to report a throwing accessor, which is the defect it reads for.
//
// Reads one definition out of the game's OWN table (`Item.GlobalItems`) and asks it the two questions
// the game's own callers ask. The game's item-table pass ends every entry with `SetTags()`, which fills
// the private `actualTags` array the accessor enumerates; a definition injected after that pass owns
// the write, and one that declares no tags used to leave the array at its CLR default (`null`), where
// `Enumerable.Contains(null, tag)` throws `ArgumentNullException`. This probe reads the array's
// PRESENCE beside the answers:
//
//   hasCarried — the first non-empty tag the definition itself carries, or the empty tag when it
//                carries none. A present array answers `true` for the empty tag (`""` splits into one
//                empty element), so this is the positive half, and it is `threw:...` on an array that
//                is absent.
//   hasAbsent  — the `gun` tag, which the game's own `PlayerCamera.HandleGunMenu` asks of the hand
//                slot every frame and which neither of this repository's fixture definitions carries,
//                so the negative half is `false` on a healthy array and `threw:...` on an absent one.
//
// Read-only: it creates nothing and writes nothing, so a run may read it on every client. No inner
// lambda captures a local: the evaluator's Mono REPL refuses that shape. One eval.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var id = {{s:id}};
	ItemInfo info;
	if (!Item.GlobalItems.TryGetValue(id, out info) || info == null) {
		return "{\"ok\":false,\"error\":\"no-definition\",\"id\":\"" + id + "\""
			+ ",\"present\":false,\"detail\":\"Item.GlobalItems carries no definition for " + id + "\"}";
	}
	var field = typeof(ItemInfo).GetField("actualTags", instanceFlags);
	var raw = field == null ? null : field.GetValue(info);
	var arrayPresent = raw != null;
	var arrayLength = raw == null ? -1 : ((System.Array)raw).Length;
	var tags = info.GetTags();
	var probe = "";
	if (tags != null) {
		for (var i = 0; i < tags.Length; i++) {
			if (!string.IsNullOrEmpty(tags[i])) { probe = tags[i]; break; }
		}
	}
	var hasCarried = "threw";
	try { hasCarried = info.HasTag(probe) ? "true" : "false"; }
	catch (System.Exception carriedError) { hasCarried = "threw:" + carriedError.GetType().Name; }
	var hasAbsent = "threw";
	try { hasAbsent = info.HasTag("gun") ? "true" : "false"; }
	catch (System.Exception absentError) { hasAbsent = "threw:" + absentError.GetType().Name; }
	return "{\"ok\":true,\"id\":\"" + id + "\",\"present\":true"
		+ ",\"arrayPresent\":" + (arrayPresent ? "true" : "false")
		+ ",\"arrayLength\":" + arrayLength.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"tags\":\"" + (tags == null ? "<null>" : string.Join(",", tags)) + "\""
		+ ",\"probeTag\":\"" + probe + "\""
		+ ",\"hasCarried\":\"" + hasCarried + "\""
		+ ",\"hasAbsent\":\"" + hasAbsent + "\"}";
}))()
