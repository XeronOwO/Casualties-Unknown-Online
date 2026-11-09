// eval declaration: run-ending-b
// declares: AcceptanceRunEndingB
// serves: 1.0-endings-authority — a diagnostic that lists what the loaded Runtime assembly actually contains.
using System;
using System.Reflection;
using System.Text;

public static class AcceptanceRunEndingB
{
	public static string Run()
	{
		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		var report = new StringBuilder();
		for (var i = 0; i < assemblies.Length; i++)
		{
			var name = assemblies[i].GetName().Name;
			if (name == null || name.IndexOf("CasualtiesUnknownOnline", StringComparison.Ordinal) < 0) { continue; }
			report.Append(name).Append('@').Append(assemblies[i].Location).Append(';');
			if (name == "CasualtiesUnknownOnline.Runtime")
			{
				var types = assemblies[i].GetTypes();
				for (var t = 0; t < types.Length; t++)
				{
					if (types[t].Name.IndexOf("RunEnding", StringComparison.Ordinal) >= 0)
					{
						report.Append('[').Append(types[t].FullName).Append(']');
					}
				}
			}
		}

		return report.ToString();
	}
}
