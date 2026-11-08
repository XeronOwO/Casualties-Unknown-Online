// eval declaration: console-history-b
// declares: AcceptanceConsoleHistoryB
// serves: console-input-keys-violate-the-minecraft-rules (the up arrow walks the history with no completion list; an empty Enter closes the window).
//
// The corrected Count lookup (arrays expose Length, lists Count); reflection only, as the first version.
using System;
using System.Reflection;

public static class AcceptanceConsoleHistoryB
{
	const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

	public static string Run()
	{
		var runtime = FindAssembly("CasualtiesUnknownOnline.Runtime");
		if (runtime == null) { return "error=no-runtime-assembly"; }
		var bootstrap = runtime.GetType("CasualtiesUnknownOnline.Runtime.CuoBootstrap", false);
		if (bootstrap == null) { return "error=no-bootstrap"; }
		var servicesProperty = bootstrap.GetProperty("Services", BindingFlags.Public | BindingFlags.Static);
		var services = servicesProperty == null ? null : servicesProperty.GetValue(null, null);
		if (services == null) { return "error=no-services"; }
		var sessionType = runtime.GetType("CasualtiesUnknownOnline.Runtime.Session.Commands.ConsoleInputSession", false);
		if (sessionType == null) { return "error=no-session-type"; }
		var getService = services.GetType().GetMethod("GetService", new[] { typeof(Type) });
		var session = getService == null ? null : getService.Invoke(services, new object[] { sessionType });
		if (session == null) { return "error=no-console-session"; }

		var open = sessionType.GetMethod("Open", Any, null, new[] { typeof(bool) }, null);
		var setInput = sessionType.GetMethod("SetInput", Any, null, new[] { typeof(string), typeof(int?) }, null);
		var submit = sessionType.GetMethod("Submit", Any);
		var previous = sessionType.GetMethod("PreviousHistory", Any);
		var close = sessionType.GetMethod("Close", Any);
		var submitOrClose = sessionType.GetMethod("SubmitOrClose", Any);
		var isOpen = sessionType.GetProperty("IsOpen", Any);
		var suggestions = sessionType.GetProperty("DisplaySuggestions", Any);

		open.Invoke(session, new object[] { true });
		setInput.Invoke(session, new object[] { "/help", null });
		submit.Invoke(session, null);
		setInput.Invoke(session, new object[] { "chat line one", null });
		submit.Invoke(session, null);
		previous.Invoke(session, null);
		var first = Describe(suggestions.GetValue(session, null));
		previous.Invoke(session, null);
		var second = Describe(suggestions.GetValue(session, null));
		close.Invoke(session, null);
		open.Invoke(session, new object[] { true });
		submitOrClose.Invoke(session, null);
		var closed = !(bool)isOpen.GetValue(session, null);
		return "listAfterFirstUp=" + first + ";listAfterSecondUp=" + second + ";emptyEnterClosed=" + (closed ? "true" : "false");
	}

	static string Describe(object list)
	{
		if (list == null) { return "null"; }
		var count = list.GetType().GetProperty("Count");
		if (count != null) { return Convert.ToString(count.GetValue(list, null)); }
		var length = list.GetType().GetProperty("Length");
		if (length != null) { return Convert.ToString(length.GetValue(list, null)); }
		return "type=" + list.GetType().Name;
	}

	static Assembly FindAssembly(string simpleName)
	{
		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++)
		{
			if (string.Equals(assemblies[i].GetName().Name, simpleName, StringComparison.Ordinal)) { return assemblies[i]; }
		}
		return null;
	}
}
