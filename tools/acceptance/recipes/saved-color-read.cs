// recipe: saved-color-read
// args: guest=s
// returns: ok, found, count, items (id/label/swatch per stored item)
//
// Reads the HOST's stored character snapshot for one peer, the exact carrier the
// cut writes to characters/<playerKey>.json. This is the diagnostic for the
// colour-persistence finding: a live clone can render a new colour while the
// stored snapshot still carries the old one.
((System.Func<string>)(() => {
	var raw = {{s:guest}};
	ulong id = 0;
	System.UInt64.TryParse(raw, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out id);
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var characters = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.CharacterData.ICharacterDataControl)) as CasualtiesUnknownOnline.Runtime.Session.CharacterData.ICharacterDataControl;
	if (characters == null) { return "{\"ok\":false,\"error\":\"no-character-control\"}"; }
	var data = characters.GetSavedCharacter(id);
	if (data == null) { return "{\"ok\":true,\"found\":false}"; }
	var stack = new System.Collections.Generic.Stack<CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterItemMsg>();
	for (var i = data.Items.Count - 1; i >= 0; i--) { stack.Push(data.Items[i]); }
	var sb = new System.Text.StringBuilder();
	sb.Append("{\"ok\":true,\"found\":true,\"count\":").Append(data.Items.Count).Append(",\"items\":[");
	var first = true;
	while (stack.Count > 0) {
		var item = stack.Pop();
		if (!first) { sb.Append(","); }
		first = false;
		sb.Append("{\"id\":\"").Append((item.ItemId ?? "").Replace("\"", "'")).Append("\",\"label\":\"").Append((item.Label ?? "").Replace("\"", "'")).Append("\",\"swatch\":").Append(item.ColorSwatch).Append("}");
		for (var c = item.Contents.Count - 1; c >= 0; c--) { stack.Push(item.Contents[c]); }
	}
	sb.Append("]}");
	return sb.ToString();
}))()
