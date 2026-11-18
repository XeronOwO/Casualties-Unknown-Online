// recipe: saved-character-read
// args: guest=s
// serves: done/id-carriers-can-disagree-about-placement.md (the host's stored character is the save
//         carrier a cut writes; rows that read a member's carried set before/after a cut need it)
// returns: ok, found, count, items (id@slot:type; children appended with their parent's slot), error
//
// Reads the HOST's stored character snapshot for one peer — ICharacterDataControl.GetSavedCharacter,
// the carrier `characters/<playerKey>.json` is written from. Run it on the host with the member's
// peer id (the id the kernel's owner fields carry). One eval.
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
	var sb = new System.Text.StringBuilder();
	sb.Append("{\"ok\":true,\"found\":true,\"count\":").Append(data.Items.Count);
	sb.Append(",\"items\":\"");
	var stack = new System.Collections.Generic.Stack<CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterItemMsg>();
	for (var i = data.Items.Count - 1; i >= 0; i--) { stack.Push(data.Items[i]); }
	while (stack.Count > 0) {
		var item = stack.Pop();
		if (sb.Length > 0 && sb[sb.Length - 1] != '"') { sb.Append(";"); }
		sb.Append(item.InstanceId).Append("@").Append(item.SlotIndex).Append(":").Append(item.ItemId);
		for (var c = item.Contents.Count - 1; c >= 0; c--) { stack.Push(item.Contents[c]); }
	}
	sb.Append("\"}");
	return sb.ToString();
}))()
