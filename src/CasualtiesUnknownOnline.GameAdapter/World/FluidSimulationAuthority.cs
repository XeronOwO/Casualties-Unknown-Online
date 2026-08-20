using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The HOST side of the fluid domain (#129): the world fluid grid is simulated
/// HERE ALONE — the game's per-side step is replaced (FluidSimulationPatch
/// intercepts FixedUpdate) by a multi-member pass over EVERY member's viewport
/// (the original drives only the local camera's, so the water around a guest
/// would stand still when the host is elsewhere). Bands are deduplicated by
/// (member chunk, y band): two members standing together must not double the
/// flow speed (the KrokMP version re-simulated overlapping chunks — water
/// flowed 2-4x faster there). The simulated viewport then streams to each
/// member: a 10 Hz changed-cells bounding box plus a 1 Hz full-viewport
/// snapshot, both ABSOLUTE RLE overwrites (idempotent — a lost message heals
/// on the next one). A member moving to a new spot invalidates its diff
/// baseline, so it gets the full viewport immediately.
/// </summary>
internal sealed class FluidSimulationAuthority(
	IWorldControl world, ISessionControl session, EntitySyncService entities, ILogger<FluidSimulationAuthority> log)
{
	private const int ViewWidth = 128;   // mirrors SimulationRange (FluidManager.cs:27-65)
	private const int ViewHeight = 112;  // y: center - 64 .. center + 48 (the 7 x 16-cell bands)
	private const int BandHeight = 16;
	private const float DiffInterval = 0.1f; // 10 Hz
	private const float FullInterval = 1.0f; // 1 Hz

	private readonly IWorldControl _world = world;
	private readonly ISessionControl _session = session;
	private readonly EntitySyncService _entities = entities;
	private readonly ILogger<FluidSimulationAuthority> _log = log;
	private readonly Dictionary<ulong, MemberView> _views = [];
	private int _simIndex;
	private byte _tileCooldown; // mirrors FluidManager.tileCooldown (the waterflow sound every 17 moves)
	private int _waterMoveCount; // mirrors FluidManager.waterMoveCount (the water-push every 11 moves)
	private float _nextDiff;
	private float _nextFull;
	private byte _seq;

	/// <summary>A member's viewport: its center (block coords) and the last SENT
	/// grid (the diff baseline — an absolute snapshot, never a delta).</summary>
	private sealed class MemberView
	{
		public Vector2Int Center;
		public byte[] Grid = new byte[ViewWidth * ViewHeight];
	}

	/// <summary>
	/// Per physical frame (the FixedUpdate patch calls this INSTEAD of the
	/// game's own step): simulate one 16-cell band of every member's viewport,
	/// deduplicated by (member chunk, y band) — overlapping members share one
	/// band pass instead of each doubling the flow speed in the overlap.
	/// </summary>
	internal void Step()
	{
		var fluid = FluidManager.main;
		var world = WorldGeneration.world;
		if (fluid == null || world == null) // Unity objects — ==
		{
			return;
		}

		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return; // guest: the grid only changes through the streamed regions
		}

		var anchors = MemberAnchors(world);
		if (anchors.Count == 0)
		{
			return;
		}

		var width = (int)world.width;
		var height = (int)world.height;
		var bands = new HashSet<(int ChunkX, int ChunkY, int Band)>();
		foreach (var pair in anchors)
		{
			var anchor = pair.Value; // net48: KeyValuePair has no Deconstruct

			var y0 = Mathf.Clamp(anchor.y + _simIndex - 64, 1, height - 3);
			if (!bands.Add((anchor.x >> 7, anchor.y >> 7, y0 / BandHeight)))
			{
				continue; // another member's band already covers this one — one pass, not two
			}

			var x0 = Mathf.Clamp(anchor.x - 64, 1, width - 3);
			var x1 = Mathf.Min(x0 + ViewWidth, width - 2);
			var y1 = Mathf.Min(y0 + BandHeight, height - 2);
			if (x1 <= x0 || y1 <= y0)
			{
				continue;
			}

			// The band's flow runs here — the game's SimulationStep is bound to
			// SimulationRangeIndex (the LOCAL camera's), so the band logic is a
			// copy of it (SimulateBand — copy source: FluidManager.cs:388-442,
			// reverse-engineering 2026-08-10, CUO convention). The host's random
			// consumption is identical to the game's own (a faithful copy), so
			// its public stream stays where the original simulation would leave it.
			SimulateBand(fluid, x0, x1, y0, y1, anchors);
		}

		_simIndex += BandHeight;
		if (_simIndex >= ViewHeight)
		{
			_simIndex = 0;
		}
	}

	/// <summary>One 128 x 16 band of the fluid simulation, copied from
	/// FluidManager.SimulationStep (FluidManager.cs:388-442) with the band range
	/// parameterized: flow down (Swap + the j &lt;= 2 evaporation + the move count
	/// driving the water-push and the waterflow sound), the 1/2 mixing, the
	/// random side spread (the PUBLIC Random stream — the host consumes it here,
	/// alone).</summary>
	private void SimulateBand(FluidManager fluid, int x0, int x1, int y0, int y1,
		Dictionary<ulong, Vector2Int> anchors)
	{
		for (var i = x0; i < x1; i++)
		{
			for (var j = y0; j < y1; j++)
			{
				if (fluid.fluid[i, j] == 0)
				{
					continue;
				}

				if (fluid.Empty(i, j - 1))
				{
					if (j <= 2)
					{
						fluid.fluid[i, j] = 0; // evaporated at the bottom
					}

					fluid.Swap(new Vector2Int(i, j), new Vector2Int(i, j - 1));
					_tileCooldown++;
					fluid.IncrMove(Vector2.down, new Vector2Int(i, j));
					SendWaterPushIfDue(fluid, new Vector2Int(i, j), Vector2.down, anchors);
					if (_tileCooldown > 16)
					{
						_tileCooldown = 0;
						if (Time.timeScale <= 1f)
						{
							var soundIndex = (byte)Random.Range(1, 4);
							Sound.Play("waterflow" + soundIndex, WorldGeneration.world.BlockToWorldPos(new Vector2Int(i, j)), false, true, null, 1f, 1f, false, false);
							SendPresentation(new Vector2Int(i, j), new FluidPresentationMsg
							{
								Kind = FluidPresentationMsg.KindWaterflowSound,
								X = i,
								Y = j,
								SoundIndex = soundIndex,
							}, anchors);
						}
					}
				}
				else
				{
					if ((fluid.fluid[i, j] == 2 && fluid.fluid[i, j - 1] == 1)
						|| (fluid.fluid[i, j] == 1 && fluid.fluid[i, j - 1] == 2))
					{
						fluid.fluid[i, j - 1] = 2;
						fluid.fluid[i, j] = 2;
					}

					var right = fluid.Empty(i + 1, j);
					var left = fluid.Empty(i - 1, j);
					if (right && left)
					{
						fluid.Swap(new Vector2Int(i, j), new Vector2Int(i + ((Random.value > 0.5f) ? 1 : -1), j));
					}
					else if (right)
					{
						fluid.Swap(new Vector2Int(i, j), new Vector2Int(i + 1, j));
						fluid.IncrMove(Vector2.right, new Vector2Int(i, j));
						SendWaterPushIfDue(fluid, new Vector2Int(i, j), Vector2.right, anchors);
					}
					else if (left)
					{
						fluid.Swap(new Vector2Int(i, j), new Vector2Int(i - 1, j));
						fluid.IncrMove(Vector2.left, new Vector2Int(i, j));
						SendWaterPushIfDue(fluid, new Vector2Int(i, j), Vector2.left, anchors);
					}
				}
			}
		}
	}

	/// <summary>Mirror FluidManager.IncrMove's water-push cadence (FluidManager.cs:232-254):
	/// after every 11 moves (the game's <c>waterMoveCount &gt; 10</c> reset) the host
	/// creates a <c>WaterPusher</c>; the guests receive the same transient as a
	/// dedicated message instead of simulating the fluid themselves.</summary>
	private void SendWaterPushIfDue(FluidManager fluid, Vector2Int pos, Vector2 direction,
		Dictionary<ulong, Vector2Int> anchors)
	{
		if (!fluid.liquidPushing)
		{
			return; // the game's IncrMove no-ops here too — no pusher on any side
		}

		_waterMoveCount++;
		if (_waterMoveCount <= 10)
		{
			return;
		}

		_waterMoveCount = 0;
		SendPresentation(pos, new FluidPresentationMsg
		{
			Kind = FluidPresentationMsg.KindWaterPush,
			X = pos.x,
			Y = pos.y,
			DirX = direction.x,
			DirY = direction.y,
		}, anchors);
	}

	/// <summary>Send one transient fluid-presentation event to every guest whose
	/// viewport contains the cell (the host's own side already ran the native
	/// effect in SimulateBand).</summary>
	private void SendPresentation(Vector2Int pos, FluidPresentationMsg msg,
		Dictionary<ulong, Vector2Int> anchors)
	{
		foreach (var pair in anchors)
		{
			var id = pair.Key;
			if (id == _session.LocalSteamId)
			{
				continue;
			}

			var center = pair.Value;
			var dx = pos.x - center.x;
			var dy = pos.y - center.y;
			if (dx >= -64 && dx < 64 && dy >= -64 && dy < 48)
			{
				_world.SendFluidPresentation(id, msg);
				_log.LogDebug("[Fluid] presentation kind={Kind} at=({X},{Y}) → {Target}.", msg.Kind, msg.X, msg.Y, id);
			}
		}
	}

	/// <summary>Per frame: stream each member's viewport — the 10 Hz changed-box
	/// diff and the 1 Hz full snapshot (the fallback: packet loss, late joiners,
	/// the bath-soiled water, members entering a new area).</summary>
	internal void Update()
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		var now = Time.time;
		if (now < _nextDiff && now < _nextFull)
		{
			return;
		}

		var world = WorldGeneration.world;
		if (world == null) // Unity object — ==
		{
			return;
		}

		foreach (var pair in MemberAnchors(world))
		{
			var id = pair.Key;
			var anchor = pair.Value; // net48: KeyValuePair has no Deconstruct
			if (id == _session.LocalSteamId)
			{
				continue; // the host's own viewport is not streamed to itself (Steam has no self-connection — thousands of failed sends and direction drops)
			}

			if (!_views.TryGetValue(id, out var view))
			{
				view = new MemberView();
				_views[id] = view;
			}

			if (view.Center != anchor)
			{
				view.Center = anchor;
				SyncRegion(id, view, 0, 0, ViewWidth, ViewHeight); // the diff baseline was for the old center — full viewport
				continue;
			}

			if (now >= _nextDiff && DiffBox(view, world) is { } box)
			{
				SyncRegion(id, view, box.X0, box.Y0, box.X1, box.Y1);
			}

			if (now >= _nextFull)
			{
				SyncRegion(id, view, 0, 0, ViewWidth, ViewHeight);
			}
		}

		if (now >= _nextDiff)
		{
			_nextDiff = now + DiffInterval;
		}

		if (now >= _nextFull)
		{
			_nextFull = now + FullInterval;
		}
	}

	/// <summary>Every member's current block position: the host's own body plus
	/// each guest's last reported position (the state stream, 20 Hz).</summary>
	private Dictionary<ulong, Vector2Int> MemberAnchors(WorldGeneration world)
	{
		var anchors = new Dictionary<ulong, Vector2Int>();
		var camera = PlayerCamera.main;
		if (camera != null && camera.body != null) // Unity objects — ==
		{
			anchors[_session.LocalSteamId] = world.WorldToBlockPos(camera.body.transform.position);
		}

		foreach (var member in _session.Members)
		{
			if (member.SteamId == _session.LocalSteamId)
			{
				continue;
			}

			var remote = _entities.GetRemotePlayer(member.SteamId);
			if (remote is null)
			{
				continue; // not in the world yet — its viewport is the initial grid, healed by the 1 Hz full on entry
			}

			anchors[member.SteamId] = world.WorldToBlockPos(new Vector2(remote.Position.X, remote.Position.Y));
		}

		return anchors;
	}

	/// <summary>The changed cells' bounding box (viewport coords) — null when the
	/// viewport matches the last sent snapshot (a quiet fluid needs no diff).</summary>
	private static (int X0, int Y0, int X1, int Y1)? DiffBox(MemberView view, WorldGeneration world)
	{
		var fluid = FluidManager.main;
		if (fluid == null) // Unity object — ==
		{
			return null;
		}

		var w = (int)world.width;
		var h = (int)world.height;
		var minX = int.MaxValue;
		var minY = int.MaxValue;
		var maxX = -1;
		var maxY = -1;
		for (var y = 0; y < ViewHeight; y++)
		{
			var gy = view.Center.y - 64 + y;
			for (var x = 0; x < ViewWidth; x++)
			{
				var gx = view.Center.x - 64 + x;
				var v = (byte)((gx >= 0 && gx < w && gy >= 0 && gy < h) ? fluid.fluid[gx, gy] : 0);
				var i = y * ViewWidth + x;
				if (v != view.Grid[i])
				{
					if (x < minX)
					{
						minX = x;
					}

					if (x > maxX)
					{
						maxX = x;
					}

					if (y < minY)
					{
						minY = y;
					}

					if (y > maxY)
					{
						maxY = y;
					}
				}
			}
		}

		return maxX < 0 ? null : (minX, minY, maxX + 1, maxY + 1);
	}

	/// <summary>Send one viewport rectangle as an ABSOLUTE RLE snapshot and fold
	/// it into the member's diff baseline (single read pass — encode + baseline
	/// together). Trailing zero runs are omitted; the receiver clears the rest.</summary>
	private void SyncRegion(ulong target, MemberView view, int x0, int y0, int x1, int y1)
	{
		var fluid = FluidManager.main;
		var world = WorldGeneration.world;
		if (fluid == null || world == null) // Unity objects — ==
		{
			return;
		}

		var w = (int)world.width;
		var h = (int)world.height;
		var runs = new List<byte>(64);
		var current = (byte)0;
		var run = 0;
		for (var y = y0; y < y1; y++)
		{
			var gy = view.Center.y - 64 + y;
			for (var x = x0; x < x1; x++)
			{
				var gx = view.Center.x - 64 + x;
				var v = (byte)((gx >= 0 && gx < w && gy >= 0 && gy < h) ? fluid.fluid[gx, gy] : 0);
				var i = y * ViewWidth + x;
				if (v == current && run < 255)
				{
					run++;
					view.Grid[i] = v;
					continue;
				}

				if (run > 0)
				{
					runs.Add(current);
					runs.Add((byte)run);
				}

				current = v;
				run = 1;
				view.Grid[i] = v;
			}
		}

		if (run > 0)
		{
			runs.Add(current);
			runs.Add((byte)run);
		}

		while (runs.Count >= 2 && runs[runs.Count - 2] == 0)
		{
			runs.RemoveRange(runs.Count - 2, 2); // the trailing zero run is implicit
		}

		_seq++;
		_world.SendFluidRegion(target, new FluidRegionMsg
		{
			Seq = _seq,
			OriginX = view.Center.x - 64 + x0,
			OriginY = view.Center.y - 64 + y0,
			Width = (byte)(x1 - x0),
			Height = (byte)(y1 - y0),
			Cells = [.. runs],
		});
		_log.LogInformation("[Fluid] region=(x={X},y={Y},w={W},h={H}) cells={N} seq={S} → {Target}.",
			view.Center.x - 64 + x0, view.Center.y - 64 + y0, x1 - x0, y1 - y0, runs.Count, _seq, target);
	}
}
