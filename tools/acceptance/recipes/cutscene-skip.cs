// recipe: cutscene-skip
// args: none
// returns: ok, before, after, error
//
// Advances the native Talker's own elapsed-talk timer to its finished value, so
// the ending cutscene's fixed dialogue waits do not dominate an acceptance run.
// This changes no cutscene decision: it only tells the Talker that the current
// line has been displayed for longer than its text needs.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags InstanceAny =
		System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var end = EndLabScript.instance;
	if (end == null) { return "{\"ok\":false,\"error\":\"no-endlab\"}"; }
	var talker = end.scientistTalker;
	if (talker == null) { return "{\"ok\":false,\"error\":\"no-talker\"}"; }
	var field = talker.GetType().GetField("timeSinceTalked", InstanceAny);
	if (field == null) { return "{\"ok\":false,\"error\":\"no-timer\"}"; }
	var before = System.Convert.ToSingle(field.GetValue(talker), System.Globalization.CultureInfo.InvariantCulture);
	field.SetValue(talker, 999f);
	return "{\"ok\":true,\"before\":" + before.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ",\"after\":999}";
}))()
