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
/// [CuoMod]-declared ICuoMod types, validates each candidate (declared id
/// non-empty, NetworkMode not Unspecified, a valid SemVer version, a valid
/// permission set for the network mode, type public + concrete + ICuoMod +
/// public parameterless constructor, no duplicated id, well-formed and
/// satisfiable dependencies) and builds the framework-owned manifests. The
/// discovered list is returned in DEPENDENCY ORDER (Kahn topological sort,
/// stable on discovery order); Stop/Dispose reuse the reverse order.
/// A rejected candidate is skipped WITH a log — one broken mod never blocks
/// the others (fail-closed per mod, not per scan). Missing dependency targets
/// reject the dependent; a dependency cycle rejects every member of the
/// cycle. Pure class, no DI services: the discovery test drives it with
/// injected assembly lists; production passes AppDomain.GetAssemblies() (the
/// first update frame — BepInEx loads plugins one by one, load-then-Awake, so
/// the scan must run after every plugin's Awake).
/// </summary>
public sealed class ModRegistry(ILogger<ModRegistry> log) : IModListProvider
{
	private readonly ILogger<ModRegistry> _log = log;
	private readonly List<DiscoveredMod> _discovered = [];

	/// <summary>Scans the given assemblies — the one and only write to the registry.</summary>
	public IReadOnlyList<DiscoveredMod> Discover(IEnumerable<Assembly> assemblies)
	{
		_discovered.Clear();
		var candidates = new List<DiscoveredMod>();
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

			if (!SemanticVersion.TryParse(attribute.Version, out _))
			{
				_log.LogWarning("[Mods] {Id} version {Version} is not a valid SemVer (major.minor.patch[-prerelease][+build]) — skipped.", id, attribute.Version);
				continue;
			}

			if (!ModPermissionPolicy.IsValidFor(attribute.NetworkMode, attribute.Permissions))
			{
				_log.LogWarning("[Mods] {Id} declares invalid permissions {Permissions} for {Mode} — skipped (unknown bits or host/state permissions on a local-only mode).",
					id, attribute.Permissions, attribute.NetworkMode);
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

			var dependencies = attribute.Dependencies ?? [];
			if (!AreDependenciesWellFormed(id, dependencies))
			{
				continue;
			}

			var manifest = new ModManifest(id, attribute.DisplayName, attribute.Version, attribute.NetworkMode,
				attribute.Description, attribute.Permissions, dependencies);
			candidates.Add(new DiscoveredMod(manifest, type));
			_log.LogInformation("[Mods] discovered {Id} {Version} ({Mode}, permissions {Permissions}) — {DisplayName}.",
				id, manifest.Version, manifest.NetworkMode, manifest.Permissions, manifest.DisplayName);
		}

		_discovered.AddRange(OrderByDependencies(candidates));

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
			Permissions = d.Manifest.Permissions,
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

	private bool AreDependenciesWellFormed(string id, string[] dependencies)
	{
		if (dependencies.Length == 0)
		{
			return true;
		}

		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var dependency in dependencies)
		{
			if (string.IsNullOrWhiteSpace(dependency))
			{
				_log.LogWarning("[Mods] {Id} declares an empty dependency id — skipped.", id);
				return false;
			}

			if (dependency == id)
			{
				_log.LogWarning("[Mods] {Id} declares itself as a dependency — skipped.", id);
				return false;
			}

			if (!seen.Add(dependency))
			{
				_log.LogWarning("[Mods] {Id} declares dependency {Dependency} twice — skipped.", id, dependency);
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Dependency resolution: a candidate whose declared dependency is not in the
	/// candidate set is rejected (the target never loaded, so the dependent must
	/// not load — fail-closed). The remaining candidates are ordered by Kahn's
	/// algorithm with a stable tie-break on discovery order; candidates left over
	/// are exactly the dependency cycles (and their downstream) and are all
	/// rejected with a log.
	/// </summary>
	private List<DiscoveredMod> OrderByDependencies(List<DiscoveredMod> candidates)
	{
		var ordered = new List<DiscoveredMod>(candidates.Count);
		if (candidates.Count == 0)
		{
			return ordered;
		}

		var byId = candidates.ToDictionary(c => c.Manifest.Id, StringComparer.Ordinal);
		var rejected = new HashSet<string>(StringComparer.Ordinal);
		foreach (var candidate in candidates)
		{
			foreach (var dependency in candidate.Manifest.Dependencies)
			{
				if (!byId.ContainsKey(dependency))
				{
					_log.LogWarning("[Mods] {Id} depends on missing mod {Dependency} — skipped.", candidate.Manifest.Id, dependency);
					rejected.Add(candidate.Manifest.Id);
					break;
				}
			}
		}

		// Closure: a mod whose own dependency was rejected is just as unsatisfied
		// as one whose dependency is missing — fail it too (transitive
		// dependencies must load, or the dependent must not).
		var changed = true;
		while (changed)
		{
			changed = false;
			foreach (var candidate in candidates)
			{
				if (rejected.Contains(candidate.Manifest.Id)
					|| !candidate.Manifest.Dependencies.Any(d => rejected.Contains(d)))
				{
					continue;
				}
				_log.LogWarning("[Mods] {Id} depends on rejected mod {Dependency} — skipped.",
					candidate.Manifest.Id, candidate.Manifest.Dependencies.First(d => rejected.Contains(d)));
				rejected.Add(candidate.Manifest.Id);
				changed = true;
			}
		}

		var remaining = candidates.Where(c => !rejected.Contains(c.Manifest.Id)).ToList();
		var indegree = remaining.ToDictionary(
			c => c.Manifest.Id,
			c => c.Manifest.Dependencies.Count(d => !rejected.Contains(d)),
			StringComparer.Ordinal);

		while (remaining.Count > 0)
		{
			// Stable order: the first zero-indegree candidate in discovery order wins.
			var nextIndex = remaining.FindIndex(c => indegree[c.Manifest.Id] == 0);
			if (nextIndex < 0)
			{
				foreach (var cycleMember in remaining)
				{
					_log.LogWarning("[Mods] {Id} is part of a dependency cycle (or depends on one) — skipped.", cycleMember.Manifest.Id);
				}

				break;
			}

			var next = remaining[nextIndex];
			ordered.Add(next);
			remaining.RemoveAt(nextIndex);
			foreach (var candidate in remaining)
			{
				if (candidate.Manifest.Dependencies.Contains(next.Manifest.Id))
				{
					indegree[candidate.Manifest.Id]--;
				}
			}
		}

		return ordered;
	}
}
