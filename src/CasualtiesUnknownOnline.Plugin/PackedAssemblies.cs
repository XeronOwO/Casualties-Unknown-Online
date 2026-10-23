using System;
using System.IO;
using System.Reflection;

namespace CasualtiesUnknownOnline;

/// <summary>
/// The resolver a PACKED single-file artifact relies on: every assembly the packaging step embedded as a
/// <c>Packed.&lt;name&gt;.dll</c> manifest resource is served from this assembly's own bytes when the runtime asks
/// for it.
///
/// <para>
/// The entry type's static constructor installs it, and that position is the whole point: the ENTRY must carry
/// no field whose signature names a packed assembly, so the entry itself loads before the resolver exists; the
/// resolver is then in place by the time <see cref="PluginHost"/> (whose fields name CUO and dependency types) is
/// loaded. In the normal deployment the assembly carries no <c>Packed.</c> resources, so every request falls
/// through (<c>null</c>) to the loader's own probing and nothing changes.
/// </para>
/// </summary>
internal static class PackedAssemblyResolver
{
	private const string ResourcePrefix = "Packed.";
	private const string ResourceSuffix = ".dll";

	/// <summary>Registers the handler and loads every embedded assembly eagerly.</summary>
	internal static void Install()
	{
		AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
		AppDomain.CurrentDomain.AssemblyResolve += Resolve;

		var assembly = typeof(PackedAssemblyResolver).Assembly;
		foreach (var name in assembly.GetManifestResourceNames())
		{
			if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
				|| !name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
			{
				continue;
			}

			using (var stream = assembly.GetManifestResourceStream(name))
			{
				if (stream is null)
				{
					continue;
				}

				using (var buffer = new MemoryStream())
				{
					stream.CopyTo(buffer);
					Assembly.Load(buffer.ToArray());
				}
			}
		}
	}

	private static Assembly? Resolve(object? sender, ResolveEventArgs args)
	{
		var requested = new AssemblyName(args.Name).Name;
		if (string.IsNullOrEmpty(requested))
		{
			return null;
		}

		var stream = typeof(PackedAssemblyResolver).Assembly
			.GetManifestResourceStream(ResourcePrefix + requested + ResourceSuffix);
		if (stream is null)
		{
			return null;
		}

		using (stream)
		using (var buffer = new MemoryStream())
		{
			stream.CopyTo(buffer);
			return Assembly.Load(buffer.ToArray());
		}
	}
}
