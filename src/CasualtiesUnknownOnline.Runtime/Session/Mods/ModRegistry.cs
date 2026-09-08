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

			// Duplicate mod ids and namespace ownership are BOTH decided after
			// dependency ordering (see ResolveIdentities): a candidate that is
			// later rejected must never consume an id or a namespace another
			// valid mod could own.
			//
			// The declared content-id namespace is validated here. Any non-null
			// declaration must be valid — an empty/whitespace value is a typo,
			// not "no namespace".
			var @namespace = attribute.Namespace;
			if (@namespace is not null)
			{
				@namespace = @namespace.ToLowerInvariant();
				if (!ContentId.IsValidNamespace(@namespace))
				{
					_log.LogWarning(
						"[Mods] {Id} declares an invalid content namespace '{Namespace}' (expected [a-z][a-z0-9_]* up to {Max} chars) — skipped.",
						id, attribute.Namespace, ContentId.MaxNamespaceLength);
					continue;
				}

				if (string.Equals(@namespace, ContentId.BuiltInNamespace, StringComparison.Ordinal))
				{
					_log.LogWarning(
						"[Mods] {Id} declares the reserved built-in content namespace '{Namespace}' — skipped.",
						id, @namespace);
					continue;
				}
			}

			var dependencies = attribute.Dependencies ?? [];
			if (!AreDependenciesWellFormed(id, dependencies))
			{
				continue;
			}

			var manifest = new ModManifest(id, attribute.DisplayName, attribute.Version, attribute.NetworkMode,
				attribute.Description, attribute.Permissions, dependencies, @namespace);
			candidates.Add(new DiscoveredMod(manifest, type));
			_log.LogInformation("[Mods] discovered {Id} {Version} ({Mode}, permissions {Permissions}, namespace {Namespace}) — {DisplayName}.",
				id, manifest.Version, manifest.NetworkMode, manifest.Permissions, @namespace ?? "-", manifest.DisplayName);
		}

		_discovered.AddRange(ClaimNamespaces(OrderByDependencies(candidates)));

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

	/// <summary>
	/// Claim at most one content namespace per surviving mod, in load
	/// (topological) order. Ownership is decided here rather than during
	/// candidate validation so a mod rejected earlier can never deny a namespace
	/// to a valid mod. A dependent of a namespace-rejected mod is dropped too,
	/// exactly like a dependent of a missing dependency (fail-closed).
	/// </summary>
	private List<DiscoveredMod> ClaimNamespaces(List<DiscoveredMod> ordered)
	{
		var owners = new Dictionary<string, string>(StringComparer.Ordinal);
		var rejectedIds = new HashSet<string>(StringComparer.Ordinal);
		var accepted = new List<DiscoveredMod>(ordered.Count);
		foreach (var candidate in ordered)
		{
			var manifest = candidate.Manifest;
			var blockedDependency = manifest.Dependencies.FirstOrDefault(rejectedIds.Contains);
			if (blockedDependency is not null)
			{
				_log.LogWarning("[Mods] {Id} depends on {Dependency}, which was rejected — skipped.",
					manifest.Id, blockedDependency);
				rejectedIds.Add(manifest.Id);
				continue;
			}

			if (manifest.Namespace is { } @namespace)
			{
				if (owners.TryGetValue(@namespace, out var owner))
				{
					_log.LogWarning(
						"[Mods] content namespace {Namespace} is already declared by {Owner} — {Id} is skipped.",
						@namespace, owner, manifest.Id);
					rejectedIds.Add(manifest.Id);
					continue;
				}

				owners.Add(@namespace, manifest.Id);
			}

			accepted.Add(candidate);
		}

		return accepted;
	}

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

		// Rejection is tracked per CANDIDATE, not per mod id: two candidates may
		// declare the same id, and a rejected one must not deny that id to the
		// other (see the duplicate-id pass below).
		var knownIds = new HashSet<string>(candidates.Select(c => c.Manifest.Id), StringComparer.Ordinal);
		var rejected = new HashSet<DiscoveredMod>();
		foreach (var candidate in candidates)
		{
			foreach (var dependency in candidate.Manifest.Dependencies)
			{
				if (knownIds.Contains(dependency))
				{
					continue;
				}

				_log.LogWarning("[Mods] {Id} depends on missing mod {Dependency} — skipped.", candidate.Manifest.Id, dependency);
				rejected.Add(candidate);
				break;
			}
		}

		// Closure: a mod whose dependency id has no surviving candidate is just
		// as unsatisfied as one whose dependency is missing — fail it too
		// (transitive dependencies must load, or the dependent must not). The
		// duplicate-id pass below never kills an id (it keeps one candidate), so
		// this closure can run before it.
		var changed = true;
		while (changed)
		{
			changed = false;
			var aliveIds = new HashSet<string>(
				candidates.Where(c => !rejected.Contains(c)).Select(c => c.Manifest.Id),
				StringComparer.Ordinal);
			foreach (var candidate in candidates)
			{
				if (rejected.Contains(candidate))
				{
					continue;
				}

				var blocked = candidate.Manifest.Dependencies.FirstOrDefault(dependency => !aliveIds.Contains(dependency));
				if (blocked is null)
				{
					continue;
				}

				_log.LogWarning("[Mods] {Id} depends on rejected mod {Dependency} — skipped.",
					candidate.Manifest.Id, blocked);
				rejected.Add(candidate);
				changed = true;
			}
		}

		// One id = one mod: the first still-loadable declaration wins, so a
		// duplicate whose twin was rejected above is kept instead of blocking
		// the id.
		var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var candidate in candidates)
		{
			if (rejected.Contains(candidate))
			{
				continue;
			}

			if (!uniqueIds.Add(candidate.Manifest.Id))
			{
				_log.LogWarning("[Mods] duplicated mod id {Id} — the later declaration is skipped (one id = one mod).",
					candidate.Manifest.Id);
				rejected.Add(candidate);
			}
		}

		var remaining = candidates.Where(c => !rejected.Contains(c)).ToList();
		var survivingIds = new HashSet<string>(remaining.Select(c => c.Manifest.Id), StringComparer.Ordinal);
		var indegree = remaining.ToDictionary(
			c => c.Manifest.Id,
			c => c.Manifest.Dependencies.Count(survivingIds.Contains),
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
