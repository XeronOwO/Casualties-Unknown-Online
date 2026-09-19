using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Host side: on the generation-finished falling edge, scan the scene for
/// every sync-domain trap entity and report the layout (the entity
/// distribution's physics queries are outside the random-stream isolation, so
/// the guests' regenerated layouts diverge — the host's scene is the
/// authority). One scan per layer: the per-layer idempotent guard resets when
/// a new generation starts.
///
/// It owns only the generation edge's RECORD: the member-facing table is
/// re-derived from the same component table at send time by
/// <see cref="LiveTrapLayoutSource"/>, so a member that enters between two
/// generation edges is never handed an entity the world has since removed.
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
		// The frame number rides the line so the generation-edge record can be
		// compared with the layer-boundary clear the Runtime logs
		// (TrapLayoutRegistry.Reset): a clear that reports dropping exactly these
		// entries and follows this line is the evidence that the record was wiped
		// in the SAME frame, which the send-time re-derive covers.
		_log.LogInformation("[TrapLayout] host scanned {Count} trap entities ({Kinds} kinds, {Keyed} runtime-created) at frame {Frame}.",
			scanned.Count, scanned.Select(s => s.Entry.Kind).Distinct().Count(), keyed, Time.frameCount);
	}
}
