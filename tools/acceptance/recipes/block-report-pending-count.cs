// recipe: block-report-pending-count
// args: none
// serves: guest-block-mutation-re-report rows 1-4, 6 and 10, the swallowed-guest-report family
// returns: ok, block, damage, drops, error, detail
//
// Reads this client's own unacknowledged guest-report counts — the three tables the fallback pump
// works from (block state, partial damage, break drops) — through WorldService's own surface. A report
// the host never answered shows up here; the host's answer, or a world/layer baseline move, takes it
// back to zero. Read-only.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var world = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.World.WorldService));
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world-service\"}"; }
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var block = world.GetType().GetProperty("PendingBlockReportCount", flags);
	var damage = world.GetType().GetProperty("PendingBlockDamageReportCount", flags);
	var drops = world.GetType().GetProperty("PendingBreakDropReportCount", flags);
	if (block == null || damage == null || drops == null) { return "{\"ok\":false,\"error\":\"no-count-property\"}"; }
	return "{\"ok\":true,\"block\":" + System.Convert.ToString(block.GetValue(world, null), inv)
		+ ",\"damage\":" + System.Convert.ToString(damage.GetValue(world, null), inv)
		+ ",\"drops\":" + System.Convert.ToString(drops.GetValue(world, null), inv) + "}";
}))()
