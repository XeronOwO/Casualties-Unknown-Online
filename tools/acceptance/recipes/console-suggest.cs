// recipe: console-suggest
// args: input=s mode=s trail=s
// serves: console-completion-hides-what-you-cannot-run
// returns: ok, mode, count, suggestions (text + description), value, error
//
// Asks the CLIENT's own completion source what it would offer for a command line, so an acceptance record
// can cite what the player sees per role: the filtered command list, /gamerule's two levels, /data's verbs.
// mode=suggest answers the candidates for the token under the caret; mode=hint answers GetHint for the same
// line. trail=space appends ONE trailing space to the input: the driver trims a whole -RecipeArg pair before
// splitting it, so a trailing space cannot survive the command line otherwise, and a fresh argument position
// is exactly what the rule-name/value and /data verb completions need (trail=none appends nothing). One
// read-only eval; the values are the source's own, never a re-implementation.
((System.Func<string>)(() => {
	var input = {{s:input}};
	var mode = {{s:mode}};
	var trail = {{s:trail}};
	if (trail == "space") { input = input + " "; }
	var escape = (System.Func<string, string>)(text => text == null ? "" : text.Replace("\\", "/").Replace("\"", "'").Replace("\r", " ").Replace("\n", " / "));
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	var source = services == null ? null : services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Commands.ICommandCompletionSource));
	if (source == null) { return "{\"ok\":false,\"error\":\"no-completion-source\"}"; }
	var completion = (CasualtiesUnknownOnline.Runtime.Session.Commands.ICommandCompletionSource)source;
	if (mode == "hint") {
		return "{\"ok\":true,\"mode\":\"hint\",\"value\":\"" + escape(completion.GetHint(input)) + "\"}";
	}

	var suggestions = completion.Suggest(input);
	var builder = new System.Text.StringBuilder();
	builder.Append("{\"ok\":true,\"mode\":\"suggest\",\"count\":").Append(suggestions.Count).Append(",\"suggestions\":[");
	for (var i = 0; i < suggestions.Count; i++) {
		if (i > 0) { builder.Append(','); }
		builder.Append("{\"text\":\"").Append(escape(suggestions[i].Text))
			.Append("\",\"description\":\"").Append(escape(suggestions[i].Description)).Append("\"}");
	}
	builder.Append("]}");
	return builder.ToString();
}))()
