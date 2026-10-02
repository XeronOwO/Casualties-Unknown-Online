// recipe: block-report-reset
// args: block=n damage=n drops=n
// serves: world-layer-generation-identity row 1 (the lost air-write shape the wire cannot stage by itself)
// returns: ok, blockBefore, damageBefore, dropsBefore, block, damage, drops, error, detail
//
// Drops this guest's own unacknowledged block-report tables through the product's own reset seams — the
// boundary path a world/layer apply runs (ResetPendingBlockReports / ResetPendingBlockDamageReports /
// ResetPendingBreakDropReports). A scenario uses it to re-create the ONE shape the production wire
// cannot produce on this machine: the air-write report is gone (its loss is staged by an inbound
// blackout) while the break's drops carrier survives and is re-reported by the fallback with the
// sender's CURRENT stamp. Each table is reset only when its argument is 1; the counts are read from the
// product's own surface before and after. Guest only — the host holds no pending report tables, so the
// call is a no-op there. One eval.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var world = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.World.WorldService)) as CasualtiesUnknownOnline.Runtime.Session.World.WorldService;
	if (world == null) { return "{\"ok\":false,\"error\":\"no-world-service\"}"; }
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var blockProperty = world.GetType().GetProperty("PendingBlockReportCount", flags);
	var damageProperty = world.GetType().GetProperty("PendingBlockDamageReportCount", flags);
	var dropsProperty = world.GetType().GetProperty("PendingBreakDropReportCount", flags);
	if (blockProperty == null || damageProperty == null || dropsProperty == null) { return "{\"ok\":false,\"error\":\"no-count-property\"}"; }
	var blockBefore = System.Convert.ToInt32(blockProperty.GetValue(world, null), inv);
	var damageBefore = System.Convert.ToInt32(damageProperty.GetValue(world, null), inv);
	var dropsBefore = System.Convert.ToInt32(dropsProperty.GetValue(world, null), inv);
	if (((int)({{n:block}})) == 1) { world.ResetPendingBlockReports(); }
	if (((int)({{n:damage}})) == 1) { world.ResetPendingBlockDamageReports(); }
	if (((int)({{n:drops}})) == 1) { world.ResetPendingBreakDropReports(); }
	return "{\"ok\":true"
		+ ",\"blockBefore\":" + blockBefore.ToString(inv)
		+ ",\"damageBefore\":" + damageBefore.ToString(inv)
		+ ",\"dropsBefore\":" + dropsBefore.ToString(inv)
		+ ",\"block\":" + System.Convert.ToInt32(blockProperty.GetValue(world, null), inv).ToString(inv)
		+ ",\"damage\":" + System.Convert.ToInt32(damageProperty.GetValue(world, null), inv).ToString(inv)
		+ ",\"drops\":" + System.Convert.ToInt32(dropsProperty.GetValue(world, null), inv).ToString(inv) + "}";
}))()
