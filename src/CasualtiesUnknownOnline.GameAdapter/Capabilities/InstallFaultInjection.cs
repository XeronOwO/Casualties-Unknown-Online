using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Patching;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Capabilities;

/// <summary>
/// The fault seam the player-visible refusal row needs: with
/// <c>CUO_FAULT_CAPABILITY</c> naming capability ids, the install reports each of
/// them as a broken hook, so a deployed build refuses exactly as it would after a
/// game update that broke that system.
///
/// <para>
/// Nothing here changes behaviour unless the variable is set, and an id the
/// catalog does not declare is logged and ignored rather than invented into a
/// refusal. The seam exists because the refusal notice's wording and placement
/// can only be read from the game's own window on a build that really refuses —
/// a unit test formats the record, it cannot show the player the line.
/// </para>
/// </summary>
internal static class InstallFaultInjection
{
	/// <summary>The environment variable: one or more capability ids, comma or semicolon separated.</summary>
	internal const string Variable = "CUO_FAULT_CAPABILITY";

	/// <summary>The failures this attempt adds, read from the process environment — empty when the variable is unset.</summary>
	internal static IReadOnlyList<PatchVerificationFailure> FromEnvironment(ILogger log) =>
		Applied(Environment.GetEnvironmentVariable(Variable), log);

	/// <summary>
	/// One blocking failure per requested capability, keyed by that capability's
	/// first declared patch class, so the catalog's Required/Optional rule and its
	/// player-facing title answer exactly as they would for a real miss.
	/// </summary>
	internal static IReadOnlyList<PatchVerificationFailure> Applied(string? requested, ILogger log)
	{
		var ids = Parse(requested);
		if (ids.Count == 0)
		{
			return [];
		}

		var failures = new List<PatchVerificationFailure>(ids.Count);
		foreach (var id in ids)
		{
			var definition = AdapterCapabilityCatalog.Find(id);
			var patchClass = AdapterCapabilityCatalog.ClassesOf(id).FirstOrDefault();
			if (definition is null || patchClass is null)
			{
				log.LogWarning("FAULT INJECTION ignores {Id}: no declared capability of that id owns a patch class.", id);
				continue;
			}

			failures.Add(new PatchVerificationFailure(
				patchClass.Name,
				patchClass.FullName ?? patchClass.Name,
				"fault injection: CUO_FAULT_CAPABILITY reports this target as not applied",
				blocksInstall: true));
			log.LogWarning(
				"FAULT INJECTION reports {Title} ({Id}) as broken on purpose; unset {Variable} to restore the real install.",
				definition.Title,
				id,
				Variable);
		}

		return failures;
	}

	/// <summary>The ids the variable names, trimmed and de-duplicated, in the order written.</summary>
	internal static IReadOnlyList<string> Parse(string? requested) =>
		string.IsNullOrWhiteSpace(requested)
			? []
			: [.. requested!
				.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
				.Select(id => id.Trim())
				.Where(id => id.Length > 0)
				.Distinct(StringComparer.OrdinalIgnoreCase)];
}
