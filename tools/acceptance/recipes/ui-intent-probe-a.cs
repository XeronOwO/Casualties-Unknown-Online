// recipe: ui-intent-probe-a
// args: control=s
// returns: ok, result, error, detail
//
// Reports one ControlInvoked intent through the surface's own ReportIntent - the
// door the window's polling uses - so the batch sound recorder can show the click
// a staged uGUI click cannot reach.
((System.Func<string>)(() =>
{
	var control = {{s:control}};
	System.Reflection.Assembly runtime = null;
	foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
	{
		if (assembly.GetName().Name == "CasualtiesUnknownOnline.Runtime")
		{
			runtime = assembly;
			break;
		}
	}

	if (runtime == null)
	{
		return "{\"ok\":false,\"error\":\"no-runtime\"}";
	}

	var bootstrap = runtime.GetType("CasualtiesUnknownOnline.Runtime.CuoBootstrap", false);
	var services = bootstrap?.GetProperty("Services", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
	System.Type adapterType = null;
	foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
	{
		if (assembly.GetName().Name == "CasualtiesUnknownOnline.GameAdapter")
		{
			adapterType = assembly.GetType("CasualtiesUnknownOnline.GameAdapter.GameAdapter", false);
			break;
		}
	}
	var getService = services?.GetType().GetMethod("GetService", new[] { typeof(System.Type) });
	var adapter = getService?.Invoke(services, new object[] { adapterType });
	if (adapter == null)
	{
		return "{\"ok\":false,\"error\":\"no-adapter\"}";
	}

	var any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
	var surface = adapter.GetType().GetField("_onlineUiSurface", any)?.GetValue(adapter);
	if (surface == null)
	{
		return "{\"ok\":false,\"error\":\"no-surface\"}";
	}

	var intentType = runtime.GetType("CasualtiesUnknownOnline.Runtime.OnlineUi.OnlineUiIntent", false);
	var kindType = runtime.GetType("CasualtiesUnknownOnline.Runtime.OnlineUi.OnlineUiIntentKind", false);
	if (intentType == null || kindType == null)
	{
		return "{\"ok\":false,\"error\":\"no-intent-type\"}";
	}

	var invoked = System.Enum.Parse(kindType, "ControlInvoked");
	var intent = System.Activator.CreateInstance(intentType, new object[] { invoked, control, "", 0f, -1, false });
	var report = surface.GetType().GetMethod("ReportIntent", any);
	if (report == null)
	{
		return "{\"ok\":false,\"error\":\"no-report-intent\"}";
	}

	report.Invoke(surface, new[] { intent });
	return "{\"ok\":true,\"result\":\"sent:" + control + "\"}";
}))()

