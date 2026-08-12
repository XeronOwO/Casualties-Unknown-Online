using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The mod discovery registry (Phase 4 Mod API): scans a set of assemblies for
/// [CuoMod]-declared ICuoMod types, validates each (declared id non-empty,
/// NetworkMode not Unspecified, type public + concrete + ICuoMod + public
/// parameterless constructor, no duplicated id across the set) and builds the
/// framework-owned manifests. A rejected candidate is skipped WITH a log — one
/// broken mod never blocks the others (fail-closed per mod, not per scan).
/// Pure class, no DI services: the discovery test drives it with injected
/// assembly lists; production passes AppDomain.GetAssemblies() (the first
/// update frame — BepInEx loads plugins one by one, load-then-Awake, so the
/// scan must run after every plugin's Awake).
/// </summary>
public sealed class ModRegistry(ILogger<ModRegistry> log) : IModListProvider
{
	private readonly ILogger<ModRegistry> _log = log;
	private readonly List<DiscoveredMod> _discovered = [];

	/// <summary>Scans the given assemblies — the one and only write to the registry.</summary>
	public IReadOnlyList<DiscoveredMod> Discover(IEnumerable<Assembly> assemblies)
	{
		_discovered.Clear();
		var seen = new HashSet<string>(StringComparer.Ordinal);

		foreach (var type in assemblies
			.SelectMany(a => SafeGetTypes(a))
			.Where(t => t.IsClass && !t.IsAbstract && (t.IsPublic || t.IsNestedPublic) && typeof(ICuoMod).IsAssignableFrom(t)))
		{
			var attribute = (CuoModAttribute?)type.GetCustomAttributes(typeof(CuoModAttribute), inherit: false).FirstOrDefault();
			if (attribute is null)
			{
				continue; // an ICuoMod without the declaration is not a mod — not ours to load
			}

			var id = attribute.Id;
			if (string.IsNullOrWhiteSpace(id))
			{
				_log.LogWarning("[Mods] {Type} declares an empty mod id — skipped.", type.FullName);
				continue;
			}

			if (attribute.NetworkMode == NetworkMode.Unspecified)
			{
				_log.LogWarning("[Mods] {Id} does not declare its NetworkMode (the [CuoMod] NetworkMode parameter) — skipped, a mod must state its network contract.", id);
				continue;
			}

			if (type.GetConstructor(Type.EmptyTypes) is null)
			{
				_log.LogWarning("[Mods] {Id} ({Type}) has no public parameterless constructor — skipped.", id, type.FullName);
				continue;
			}

			if (!seen.Add(id))
			{
				_log.LogWarning("[Mods] duplicated mod id {Id} — the later declaration is skipped (one id = one mod).", id);
				continue;
			}

			var manifest = new ModManifest(id, attribute.DisplayName, attribute.Version, attribute.NetworkMode, attribute.Description);
			_discovered.Add(new DiscoveredMod(manifest, type));
			_log.LogInformation("[Mods] discovered {Id} {Version} ({Mode}) — {DisplayName}.", id, manifest.Version, manifest.NetworkMode, manifest.DisplayName);
		}

		if (_discovered.Count == 0)
		{
			_log.LogInformation("[Mods] no CUO mods found.");
		}

		return _discovered;
	}

	/// <summary>The discovered mods as handshake infos (empty before discovery ran).</summary>
	public List<ModInfoMsg> CurrentModInfos() =>
		[.. _discovered.Select(d => new ModInfoMsg
		{
			Id = d.Manifest.Id,
			Version = d.Manifest.Version,
			NetworkMode = d.Manifest.NetworkMode,
		})];

	private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException e)
		{
			// The loadable types of a partially-loadable assembly (the unloadable
			// ones are null entries) — a mod DLL with an unresolvable dependency
			// must not take the whole scan down with it.
			return e.Types.OfType<Type>();
		}
	}
}
