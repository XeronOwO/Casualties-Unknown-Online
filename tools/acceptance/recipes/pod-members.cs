// recipe: pod-members
// args: none
// serves: pod-team-wait
// returns: ok, localX, localY, members[{steamId,x,y}], error
//
// Reads the host's replicated entity positions. The drill-pod gate reads this table,
// so a failed trigger must say whether the host ever saw the member at the pod.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var syncType = System.Type.GetType("CasualtiesUnknownOnline.Runtime.Session.EntitySync.IEntitySyncControl, CasualtiesUnknownOnline.Runtime");
	var sync = syncType == null ? null : services.GetService(syncType) as CasualtiesUnknownOnline.Runtime.Session.EntitySync.IEntitySyncControl;
	if (sync == null) { return "{\"ok\":false,\"error\":\"no-entity-sync\"}"; }
	var local = sync.LocalPlayer;
	var builder = new System.Text.StringBuilder();
	builder.Append("{\"ok\":true,\"localX\":").Append(local.Position.X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
	builder.Append(",\"localY\":").Append(local.Position.Y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
	builder.Append(",\"members\":[");
	var first = true;
	foreach (var member in sync.RemotePlayers) {
		if (!first) { builder.Append(','); }
		first = false;
		builder.Append("{\"steamId\":\"").Append(member.SteamId.ToString(System.Globalization.CultureInfo.InvariantCulture));
		builder.Append("\",\"x\":").Append(member.Position.X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
		builder.Append(",\"y\":").Append(member.Position.Y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append('}');
	}
	builder.Append("]}");
	return builder.ToString();
}))()
