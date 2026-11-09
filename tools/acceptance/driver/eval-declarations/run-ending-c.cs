// eval declaration: run-ending-c
// declares: AcceptanceRunEndingC
// serves: 1.0-endings-authority — names what the loaded Runtime assembly contains, path-free.
using System;
using System.Reflection;
using System.Text;

public static class AcceptanceRunEndingC
{
	public static string Run()
	{
		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++)
		{
			if (assemblies[i].GetName().Name != "CasualtiesUnknownOnline.Runtime") { continue; }
			var types = assemblies[i].GetTypes();
			var hits = new StringBuilder();
			var count = 0;
			for (var t = 0; t < types.Length; t++)
			{
				if (types[t].Name.IndexOf("RunEnding", StringComparison.Ordinal) >= 0)
				{
					hits.Append(types[t].FullName).Append(';');
					count++;
				}
			}

			return "types=" + types.Length + "|runEndingHits=" + count + "|" + hits;
		}

		return "no-runtime-assembly";
	}
}
