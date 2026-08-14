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
			_world.ReportTrapLayout(entity.Entry.Kind, entity.Entry.X, entity.Entry.Y, entity.Entry.PrefabName);
		}

		_log.LogInformation("[TrapLayout] host scanned {Count} trap entities ({Kinds} kinds).",
			scanned.Count, scanned.Select(s => s.Entry.Kind).Distinct().Count());
	}
}
