using System;
using System.Linq;
using CasualtiesUnknownOnline.GameAdapter.Capabilities;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
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
	internal bool Install()
	{
		try
		{
			_capabilities.BeginInstall();
			_harmony = new Harmony("CasualtiesUnknownOnline.GameAdapter");
			_harmony.PatchAll(typeof(PatchInventory).Assembly);
			_capabilities.Record(DynamicPatchInstaller.Install(_harmony, _log));

			// Never let a failed patch silently run: verify every patch class
			// actually landed on its target (a game update that breaks a target
			// must fail loud — a silently missing hook is how sync bugs hide).
			var missing = PatchInventory.VerifyMissing(_harmony);
			_capabilities.Record(missing);
			if (AdapterCapabilityReport.RefusesInstall(missing))
			{
				_log.LogError("Game Adapter patch verification FAILED — {Count} targets not applied: {Missing}",
					missing.Count, string.Join(", ", missing.Select(failure => failure.Detail)));
				_harmony.UnpatchSelf();
				_harmony = null;
				return false;
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
