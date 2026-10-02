// recipe: block-report-cap-probe
// args: none
// serves: guest-block-mutation-re-report row 8 (the pending table's cap and its overflow warning)
// returns: ok, cap, filled, atCap, refused, latch, updateExisting, cleared, latchAfterReset, error, detail
//
// Exercises the pending block-report table's cap policy on an ISOLATED instance of the product's own
// bookkeeping type: a live 65 536-entry fill would arm the fallback pump and put 65 536 reports on the
// wire, so the live table is never touched. The probe fills the instance through the table's own
// Report, then hands one more NEW cell to the bookkeeping's own Report — the path that logs the
// once-per-episode warning and never drops silently — and finally runs Reset() exactly as a baseline
// move does. The warning and the reset account line land in the client's own log through the real
// logger. That the fill reaches the real bound is the point: 65 536 is the product's own cap constant.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var logger = services.GetService(typeof(Microsoft.Extensions.Logging.ILogger<CasualtiesUnknownOnline.Runtime.Session.World.WorldService>));
	if (logger == null) { return "{\"ok\":false,\"error\":\"no-logger\"}"; }
	var type = System.Type.GetType("CasualtiesUnknownOnline.Runtime.Session.World.GuestBlockReportBookkeeping, CasualtiesUnknownOnline.Runtime", false);
	if (type == null) { return "{\"ok\":false,\"error\":\"no-bookkeeping-type\"}"; }
	object bookkeeping;
	try {
		bookkeeping = System.Activator.CreateInstance(
			type,
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
			null,
			new object[] { logger },
			null);
	}
	catch (System.Exception exception) {
		return "{\"ok\":false,\"error\":\"bookkeeping-ctor\",\"detail\":\"" + exception.GetType().Name + "\"}";
	}
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var tableField = type.GetField("_table", flags);
	var latchField = type.GetField("_overflowLogged", flags);
	var reportMethod = type.GetMethod("Report", flags, null, new System.Type[] { typeof(int), typeof(int), typeof(ushort), typeof(bool) }, null);
	var resetMethod = type.GetMethod("Reset", flags);
	if (tableField == null || latchField == null || reportMethod == null || resetMethod == null) {
		return "{\"ok\":false,\"error\":\"no-member\"}";
	}
	var table = tableField.GetValue(bookkeeping) as CasualtiesUnknownOnline.Runtime.Session.World.PendingBlockReportTable;
	if (table == null) { return "{\"ok\":false,\"error\":\"no-table\"}"; }
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var cap = table.Cap;
	var filled = 0;
	for (var i = 0; i < cap && table.Count < cap; i++) {
		if (table.Report(i % 4096, i / 4096, 1, true)) { filled++; }
	}
	var atCap = table.Count;
	reportMethod.Invoke(bookkeeping, new object[] { cap + 1, cap + 1, (ushort)1, true });
	var refused = table.Count == cap;
	var latch = System.Convert.ToBoolean(latchField.GetValue(bookkeeping));
	var updateExisting = table.Report(0, 0, 2, true);
	resetMethod.Invoke(bookkeeping, null);
	return "{\"ok\":true"
		+ ",\"cap\":" + cap.ToString(inv)
		+ ",\"filled\":" + filled.ToString(inv)
		+ ",\"atCap\":" + atCap.ToString(inv)
		+ ",\"refused\":" + (refused ? "true" : "false")
		+ ",\"latch\":" + (latch ? "true" : "false")
		+ ",\"updateExisting\":" + (updateExisting ? "true" : "false")
		+ ",\"cleared\":" + table.Count.ToString(inv)
		+ ",\"latchAfterReset\":" + (System.Convert.ToBoolean(latchField.GetValue(bookkeeping)) ? "true" : "false") + "}";
}))()
