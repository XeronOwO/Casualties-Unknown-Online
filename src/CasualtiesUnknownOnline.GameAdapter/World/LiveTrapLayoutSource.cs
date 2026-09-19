using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The Game Adapter's <see cref="ILiveTrapLayoutSource"/>: the scan that
/// re-derives the host's member-facing trap-layout table from the LIVE scene at
/// send time. It shares the component table with the generation-edge scanner
/// (<see cref="TrapEntityScan"/>); only the trigger differs.
///
/// Three rules make the repeat safe:
/// <list type="bullet">
/// <item>a layer that is still generating is not scanned — the scene is
/// mid-rebuild, and the generation-finished edge's own scan owns that
/// moment;</item>
/// <item>a repeat inside ONE frame is not scanned again: two scans in the same
/// frame would see the same scene for practical purposes (an object instantiated
/// later in that frame is the exception, and the next scan sees it), and the
/// in-session repair wave calls this once per in-world member inside one frame, so
/// a wave costs ONE scan however many members it heals (a scan is one
/// <c>FindObjectsOfType</c> pass per sync-domain component type, not a cheap
/// lookup);</item>
/// <item>an EMPTY scan is handed to the registry as it is —
/// <see cref="TrapLayoutRegistry.Replace"/> refuses it against a non-empty table,
/// because inactive and unloaded objects are invisible to the scan and "the whole
/// layer's traps were destroyed at once" is far less likely than a scene in
/// transition. The fail-safe direction is a stale entry, corrected by the next scan
/// that sees entities, rather than a mass destroy on every peer.</item>
/// </list>
///
/// Public because the plugin's composition root registers the port for the
/// Runtime's fan-out: the adapter is the only layer that can scan the scene, and
/// the Runtime cannot reference the adapter.
/// </summary>
public sealed class LiveTrapLayoutSource(ISessionControl session, IWorldControl world, ILogger<LiveTrapLayoutSource> log) : ILiveTrapLayoutSource
{
	private readonly ISessionControl _session = session;
	private readonly IWorldControl _world = world;
	private readonly ILogger<LiveTrapLayoutSource> _log = log;

	private int _lastScannedFrame = -1;

	public void RefreshFromLiveScene()
	{
		if (_session.Role != SessionRole.Host || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		var frame = Time.frameCount; // Unity: the frame the scene was last read in
		if (frame == _lastScannedFrame)
		{
			return;
		}

		_lastScannedFrame = frame;

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
