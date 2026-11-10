using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Capabilities;
using CasualtiesUnknownOnline.Runtime.Patching;
using HarmonyLib;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The adapter's patch-install life cycle: own the Harmony instance, apply the
/// assembly's patches plus the hand-declared dynamic ones, verify that every
/// target actually landed, refuse the install as a whole when a blocking failure
/// was found (rollback included) and publish the attempt's capability report.
/// Split out of GameAdapter at the 600-line gate — "install and verify the patch
/// set" is one responsibility, and the capability catalog made it big enough to
/// name.
/// </summary>
internal sealed class PatchInstallLifecycle
{
	private readonly ILogger _log;
	private readonly AdapterCapabilityReporter _capabilities;
	private Harmony? _harmony;

	/// <summary>
	/// The REQUIRED capabilities whose hooks did not land in the last attempt,
	/// by their player-facing titles — what the mod's own window tells the player.
	/// Empty when the install succeeded (an Optional degradation is not a refusal).
	/// </summary>
	internal IReadOnlyList<string> RefusedCapabilities { get; private set; } = [];

	internal PatchInstallLifecycle(ILogger log)
	{
		_log = log;
		_capabilities = new AdapterCapabilityReporter(log);
	}

	/// <summary>
	/// The ProbeGame verdict and its report text, rendered from the capability
	/// catalog's declared game-probe types. The verdict and the text are unchanged
	/// from the four <c>typeof</c> reads this replaces: a compile-time reference
	/// cannot fail, so stage 2 is where a probe result starts meaning something.
	/// </summary>
	internal static bool ProbeGame(out string report)
	{
		var types = AdapterCapabilityCatalog.GameProbeTypes;
		var ok = types.All(type => type is not null);
		report = string.Join("/", types.Select(type => type.Name)) + (ok ? ": OK" : ": MISSING");
		return ok;
	}

	/// <summary>
	/// True when the whole patch set landed and was verified. Stage 1 keeps the
	/// ALL-OR-NOTHING rule whatever a capability's Required/Optional class says;
	/// the failure facts are recorded for the report either way, and a refusal
	/// unpatches before returning false so a half-applied set never runs.
	/// </summary>
	internal bool Install(IReadOnlyCollection<string>? disabledCapabilities = null)
	{
		try
		{
			_capabilities.BeginInstall();
			_harmony = new Harmony("CasualtiesUnknownOnline.GameAdapter");
			_harmony.PatchAll(typeof(PatchInventory).Assembly);
			_capabilities.Record(DynamicPatchInstaller.Install(_harmony, _log));

			// Stage 3: an OPTIONAL capability can be left out as a whole unit; a
			// Required one (or an unknown id) is refused loudly and installs.
			var selection = AdapterCapabilitySelection.Resolve(disabledCapabilities);
			if (selection.Refused.Count > 0)
			{
				_log.LogWarning("Game Adapter ignores disabled capability request(s) that are unknown or Required: {Ids}",
					string.Join(", ", selection.Refused));
			}

			var disabledClasses = new HashSet<Type>(selection.Disabled.SelectMany(AdapterCapabilityCatalog.ClassesOf));
			if (disabledClasses.Count > 0)
			{
				var removedClasses = new SortedSet<string>(StringComparer.Ordinal);
				var removedPatches = 0;
				foreach (var method in _harmony.GetPatchedMethods().ToList())
				{
					var info = Harmony.GetPatchInfo(method);
					if (info is null)
					{
						continue;
					}

					// Unpatch the disabled unit's OWN patch methods only. Removing every
					// patch on a touched method also took a sibling capability's hook that
					// shares the target (2026-11-10: the diagnostics unit's pick-up hook
					// shares Body.PickUpItem with a required drag hook, and the wholesale
					// unpatch made the required hook report as not applied).
					foreach (var patch in info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers))
					{
						if (patch.PatchMethod.DeclaringType is { } owner && disabledClasses.Contains(owner))
						{
							_harmony.Unpatch(method, patch.PatchMethod);
							removedPatches++;
							removedClasses.Add(owner.Name);
						}
					}
				}

				_log.LogInformation("Game Adapter installed with {Count} capability unit(s) off by configuration: {Ids}",
					selection.Disabled.Count, string.Join(", ", selection.Disabled));
				_log.LogInformation("Game Adapter removed {Patches} patch method(s) of {Classes} class(es) from the registry: {Names}",
					removedPatches, removedClasses.Count, string.Join(", ", removedClasses));
			}

			// Never let a failed patch silently run: verify every patch class
			// actually landed on its target (a game update that breaks a target
			// must fail loud — a silently missing hook is how sync bugs hide).
			var disabledNames = new HashSet<string>(disabledClasses.Select(container => container.Name), StringComparer.Ordinal);
			var missing = PatchInventory.VerifyMissing(_harmony)
				.Where(failure => !disabledNames.Contains(failure.PatchClass))
				.ToList();
			_capabilities.Record(missing);

			// Stage 2: an OPTIONAL capability's missing hook installs as off — the
			// report names it, the session runs without it. A REQUIRED capability's
			// missing hook still refuses the whole session (and unpatches), and a
			// failure no capability claims keeps that rule too, because a catalog
			// gap must never turn a broken hook into a silent degradation.
			var blocking = missing.Where(failure => AdapterCapabilityCatalog.IsRequiredOwner(failure.PatchClass)).ToList();
			if (blocking.Count > 0)
			{
				RefusedCapabilities = [.. blocking
					.Select(failure => AdapterCapabilityCatalog.TitleOfPatchClass(failure.PatchClassType))
					.Where(title => title is not null)
					.Select(title => title!)
					.Distinct(StringComparer.Ordinal)];
				_log.LogError("Game Adapter patch verification FAILED — {Count} required target(s) not applied: {Missing}",
					blocking.Count, string.Join(", ", blocking.Select(failure => failure.Detail)));
				_harmony.UnpatchSelf();
				_harmony = null;
				return false;
			}

			RefusedCapabilities = [];

			if (missing.Count > 0)
			{
				_log.LogWarning("Game Adapter installed with {Count} optional capability hook(s) off: {Missing}",
					missing.Count, string.Join(", ", missing.Select(failure => failure.Detail)));
			}

			_log.LogInformation("Game Adapter patches installed and verified ({Count} targets).", PatchInventory.CountTargets());
			return true;
		}
		catch (Exception ex)
		{
			// The throw IS this attempt's failure fact: without it the capability report
			// would print an all-OK capability list for a refused install.
			_log.LogError(ex, "Game Adapter patch install failed.");
			_capabilities.Record([InstallFailure(ex)]);
			_harmony?.UnpatchSelf();
			_harmony = null;
			return false;
		}
	}

	/// <summary>The install attempt's own failure — a throw means no capability report may call the session healthy.</summary>
	private static PatchVerificationFailure InstallFailure(Exception ex) =>
		new("(install)", "(install)", $"the patch install threw {ex.GetType().Name}: {ex.Message}", blocksInstall: true);

	internal void Uninstall()
	{
		_harmony?.UnpatchSelf();
		_harmony = null;
	}

	/// <summary>Publish the attempt's aggregated capability report; the caller passes the game probe's own line.</summary>
	internal void Publish(string gameProbeLine) => _capabilities.Publish(gameProbeLine);
}


