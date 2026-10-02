// recipe: runtime-entity-tables
// args: none
// serves: runtime-entity-spawn-backfill rows 1, 2, 11 and 12, runtime-entity-creation-rejection rows 1-5 and 7-9
// returns: ok, role, accepted, animals, cap, pending, error, detail
//
// Reads this client's own runtime-entity recovery tables through the product's own surfaces: the host's
// accepted-creation table (RuntimeEntityRegistry) and the guest's unacknowledged-report table
// (RuntimeEntityChannel.PendingEntityReportCount, internal — read by reflection). A report the host has
// not answered shows up in `pending`; the host's answer (its relay echo or the absolute snapshot), the
// entity's death, or a world/layer move takes it back to zero. Read-only.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var registry = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.World.RuntimeEntityRegistry));
	var channel = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.World.RuntimeEntityChannel));
	if (registry == null || channel == null) { return "{\"ok\":false,\"error\":\"no-entity-services\"}"; }
	var pendingProperty = channel.GetType().GetProperty("PendingEntityReportCount", flags);
	var countProperty = registry.GetType().GetProperty("Count", flags);
	var animalProperty = registry.GetType().GetProperty("AnimalCount", flags);
	var capProperty = registry.GetType().GetProperty("Cap", flags);
	if (pendingProperty == null || countProperty == null || animalProperty == null || capProperty == null) {
		return "{\"ok\":false,\"error\":\"no-table-property\"}";
	}
	var role = "unknown";
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.ISessionControl)) as CasualtiesUnknownOnline.Runtime.Session.ISessionControl;
	if (session != null) { role = session.Role.ToString(); }
	return "{\"ok\":true,\"role\":\"" + role + "\""
		+ ",\"accepted\":" + System.Convert.ToString(countProperty.GetValue(registry, null), inv)
		+ ",\"animals\":" + System.Convert.ToString(animalProperty.GetValue(registry, null), inv)
		+ ",\"cap\":" + System.Convert.ToString(capProperty.GetValue(registry, null), inv)
		+ ",\"pending\":" + System.Convert.ToString(pendingProperty.GetValue(channel, null), inv) + "}";
}))()
