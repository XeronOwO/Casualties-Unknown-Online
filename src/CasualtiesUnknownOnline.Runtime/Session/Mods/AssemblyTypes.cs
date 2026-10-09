using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The one answer to "which types can this assembly load": a mod DLL whose
/// dependency is missing loads partially, and the runtime hands back a
/// <see cref="ReflectionTypeLoadException"/> whose <c>Types</c> array carries
/// the loadable half with nulls where the rest failed. A scan that let that
/// exception out would take the whole discovery down over one broken mod, so
/// both the mod scan and the content-declaration census read their types here.
/// </summary>
internal static class AssemblyTypes
{
	/// <summary>The assembly's loadable types — every type when the assembly loads whole.</summary>
	internal static IReadOnlyList<Type> Loadable(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException e)
		{
			return [.. e.Types.OfType<Type>()];
		}
	}
}
