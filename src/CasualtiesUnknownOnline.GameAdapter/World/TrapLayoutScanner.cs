using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Host side: on the generation-finished falling edge, scan the scene for
/// every sync-domain trap entity and report the layout (the entity
/// distribution's physics queries are outside the random-stream isolation, so
/// the guests' regenerated layouts diverge — the host's scene is the
/// authority). One scan per layer: the per-layer idempotent guard resets when
/// a new generation starts.
/// </summary>
internal sealed class TrapLayoutScanner(ISessionControl session, IWorldControl world, ILogger<TrapLayoutScanner> log)
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly ILogger<TrapLayoutScanner> _log = log;

	private bool _generating;
	private bool _scanned;

	/// <summary>Pump: detect the generation-finished falling edge and scan once per layer.</summary>
	internal void Update()
	{
		var generating = HarmonyTraverse.IsGenerating();
		if (generating && !_generating)
		{
			_scanned = false; // a new layer is generating — its layout is new
		}

		if (!generating && _generating && _session.Role == SessionRole.Host && !_scanned)
		{
			_scanned = true;
			Scan();
		}

		_generating = generating;
	}

	private void Scan()
	{
		var scanned = TrapEntityScan.Scan();
		foreach (var entity in scanned)
		{
			_world.ReportTrapLayout(entity.Entry.Kind, entity.Entry.X, entity.Entry.Y, entity.Entry.PrefabName, entity.Entry.CreationKey);
		}

		var keyed = scanned.Count(s => s.Entry.CreationKey is not null);
		_log.LogInformation("[TrapLayout] host scanned {Count} trap entities ({Kinds} kinds, {Keyed} runtime-created).",
			scanned.Count, scanned.Select(s => s.Entry.Kind).Distinct().Count(), keyed);
	}

	/// <summary>
	/// Host only: re-derive the table from the LIVE scene. The in-session repair
	/// calls this right before it re-sends the layout, so a trap the world has
	/// since removed (a self-destructed turret, a broken crystal) is gone from
	/// the snapshot instead of being re-materialized on every peer every cycle.
	/// Skipped while a layer is generating — the generation edge's own failing-edge
	/// scan owns that moment, and the registry refuses an empty scan against a
	/// non-empty table (a scene in transition is not a destroyed layer).
	/// </summary>
	internal void RefreshLayout()
	{
		if (_session.Role != SessionRole.Host || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		var scanned = TrapEntityScan.Scan();
		if (!_world.ReplaceTrapLayout([.. scanned.Select(s => s.Entry)]))
		{
			// The refusing branch is a decision, not a no-op: record it, so a
			// field report of "the guests kept traps the host had removed" has a
			// line naming the fail-safe instead of only a "0 entries" refresh log.
			_log.LogWarning("[TrapLayout] live-scene refresh REFUSED ({Count} scanned): the table still holds entries, so it is kept — a scene in transition must not wipe every guest's layout. It clears at the next generation edge.", scanned.Count);
			return;
		}

		_log.LogInformation("[TrapLayout] host layout re-derived from the live scene: {Count} entries.", scanned.Count);
	}
}
