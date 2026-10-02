using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Logger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The deferred landing of authority rows whose generation-time object had not appeared yet when
/// the row was applied. Every apply path (the world-entry repair's per-row commands, the generation
/// publish, the periodic keyframe) lands through <see cref="RemoteItemSceneOps.SpawnWorldItem"/>,
/// and while this side's own generation may still be registering its last objects — measured from
/// the IsGenerating falling edge plus a grace — a row with nothing to adopt is deferred instead of
/// materialized beside the object that is about to arrive. Every frame it retries the deferred rows
/// against the scene, adopting one as soon as its object appears and materializing it when the grace
/// expires, so an authority row can never be lost to a slow local load. The scene access stays with
/// the caller (the delegates): this class only owns the queue, the edge timestamp and the decision.
/// </summary>
internal sealed class DeferredWorldItemLanding(
	Func<WorldItem, bool> tryAdopt,
	Func<ulong, bool> exists,
	Action<WorldItem> materialize,
	Logger log)
{
	private readonly Func<WorldItem, bool> _tryAdopt = tryAdopt;
	private readonly Func<ulong, bool> _exists = exists;
	private readonly Action<WorldItem> _materialize = materialize;
	private readonly Logger _log = log;

	private readonly PendingWorldItemRows _rows = new();
	private readonly List<WorldItem> _waiting = []; // scratch: the rows the per-frame retry pass walks
	private readonly List<WorldItem> _expired = []; // scratch: the rows that outlived their grace this frame
	private readonly List<WorldItem> _probe = []; // scratch: the sweep's "is a row waiting for this object" query (its own list — it can run while a pump walk is live)
	private bool _wasGenerating;
	private double _generationEndedAt = double.NegativeInfinity;

	/// <summary>How long a deferred row waits for its local generation-time object before the authority's own copy is materialized. Two seconds covers the post-generation landing window measured in batch 20261002-j (the missing objects appeared within about a second after the table was applied) without holding an authoritative item back noticeably.</summary>
	private const double GraceSeconds = 2.0;

	/// <summary>True while this side's world generation may still be registering objects: measured from the IsGenerating falling edge (a generation that never ran leaves the timestamp at its initial negative infinity).</summary>
	internal bool IsGenerationStillLanding() => Time.realtimeSinceStartup - _generationEndedAt < GraceSeconds;

	/// <summary>Defer one row. Idempotent per item id — a repeated snapshot refreshes the payload but keeps the original deadline. Returns true when the row is new (the caller reports the first sighting).</summary>
	internal bool Defer(WorldItem w) => _rows.AddOrRefresh(w, Time.realtimeSinceStartup);

	/// <summary>Drop every deferred row: the session ended or the world was replaced, so the rows describe a table the next scene must not materialize (the same rule GeneratedItemApplication.Unbind states for its held snapshot). The drop is reported when it costs rows — a deferred row that never landed is otherwise invisible (batch 20261002-j's second review, minor-1).</summary>
	internal void Clear()
	{
		if (_rows.Count > 0)
		{
			_log.LogInformation("[ItemSpawn] {Count} deferred row(s) dropped: the session ended or the world was replaced before they landed.", _rows.Count);
		}

		_rows.Clear();
	}

	/// <summary>
	/// True when a row is still waiting for THIS local object — same definition, inside the adopt
	/// tolerance. The keyframe's late-local sweep asks before it drops an id-less world item: an
	/// object a deferred row is holding out for is that row's adopt target, and dropping it makes the
	/// row materialize the authority's copy after all (batch 20261002-j's review: the sweep ran
	/// before the adopt pass and ate the six objects the deferred rows were waiting for, so
	/// `adopted` stayed zero in every run).
	/// </summary>
	internal bool IsWaitingFor(Item item)
	{
		if (_rows.Count == 0)
		{
			return false;
		}

		_rows.CopyWaiting(_probe);
		var at = item.transform.position;
		foreach (var w in _probe)
		{
			if (w.Item.ItemId != item.id)
			{
				continue;
			}

			var dx = w.Pos.X - at.x;
			var dy = w.Pos.Y - at.y;
			if ((dx * dx) + (dy * dy) <= RemoteItemSceneOps.AdoptToleranceSquared)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>True when a row for this instance id is still waiting — the landing seam could not place it yet, so it is DEFERRED rather than refused.</summary>
	internal bool IsWaitingFor(ulong itemId) => _rows.Contains(itemId);

	/// <summary>
	/// Pump, once per frame: track the generation falling edge, then retry every deferred row — a
	/// row whose local object has appeared is adopted; a row past its grace is materialized the
	/// ordinary way. The whole set is dropped when this side has no world scene: the rows describe
	/// the world that is gone, and the next world delivers its own table (repair / keyframe).
	/// </summary>
	internal void Update()
	{
		var generating = HarmonyTraverse.IsGenerating();
		if (_wasGenerating && !generating)
		{
			_generationEndedAt = Time.realtimeSinceStartup;
		}

		_wasGenerating = generating;

		if (_rows.Count == 0)
		{
			return;
		}

		if (!HarmonyTraverse.HasWorld)
		{
			_log.LogInformation("[ItemSpawn] {Count} deferred row(s) dropped: no world scene on this side.", _rows.Count);
			_rows.Clear();
			return;
		}

		// The world object exists but is not usable yet (a generation is running, or the entry is
		// still bringing the camera up): keep the rows and touch nothing. Materializing here would
		// land an authority row in a scene that cannot host it — the race SpawnWorldItem's own guards
		// refuse, and which this expiry path used to bypass (batch 20261002-j's review, major-2).
		if (!HarmonyTraverse.HasLiveWorld)
		{
			return;
		}

		_rows.CopyWaiting(_waiting);
		var adopted = 0;
		foreach (var w in _waiting)
		{
			if (_exists(w.ItemId))
			{
				_rows.Remove(w.ItemId); // landed through another path
				continue;
			}

			// A container row waits for its parent first: the child copy is not what has to
			// land, the parent object is. A missing parent keeps the row deferred — the same
			// grace covers it.
			if (w.ParentItemId != 0 && !_exists(w.ParentItemId))
			{
				continue;
			}

			if (_tryAdopt(w))
			{
				_rows.Remove(w.ItemId);
				adopted++;
			}
		}

		_rows.TakeExpired(Time.realtimeSinceStartup, GraceSeconds, _expired);
		foreach (var w in _expired)
		{
			if (!_exists(w.ItemId))
			{
				_materialize(w); // no local object ever appeared — the authority's copy lands
			}
		}

		if (adopted > 0 || _expired.Count > 0)
		{
			_log.LogInformation("[ItemSpawn] deferred rows: adopted {Adopted} late, materialized {Expired} after {Grace:F1} s without a local copy.",
				adopted, _expired.Count, GraceSeconds);
		}
	}
}
