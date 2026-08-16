using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.GameAdapter.Patches;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// World-event domain: block placement + the generated-baseline difference
/// table, building-entity damage/open, earthquakes (host timing — guests never
/// trigger, they only receive) and the keypad codes. Block DAMAGE + the block
/// break arbitration live in <see cref="BlockBreakSync"/> (the break's drops
/// are one message with the break — split for the 600-line gate). Local
/// compute → report → host relay/arbitration, position-keyed. Owns the
/// deferred spawn-landing presentation (sound + camera shake) that the start
/// gate must not play into the frozen world.
/// </summary>
internal sealed class WorldEventSync(
	SessionService session,
	WorldService world,
	BlockBreakSync blockBreaks,
	OperationTrace trace,
	ILogger<WorldEventSync> log)
{
	private readonly SessionService _session = session;
	private readonly WorldService _world = world;
	private readonly BlockBreakSync _blockBreaks = blockBreaks;
	private readonly OperationTrace _trace = trace;
	private readonly ILogger<WorldEventSync> _log = log;

	/// <summary>True while a remote world mutation is being applied — the local-report hooks must stay silent (call identity lives in CallContext, not bools).</summary>
	private bool IsRemoteApply => CallContext.Current == CallContext.Origin.RemoteApply;

	/// <summary>The generated world snapshot the difference table diffs against (host/solo only).</summary>
	private ushort[,]? _baseline;

	internal void BindToSession()
	{
		_world.BlockDamagedReceived += _blockBreaks.OnRemoteBlockDamaged;
		_world.BuildingEntityDamagedReceived += OnRemoteBuildingEntityDamaged;
		_world.BuildingEntityOpenedReceived += OnRemoteBuildingEntityOpened;
		_world.BlockStateReceived += OnRemoteBlockState;
		_world.BlockPlacedReceived += OnRemoteBlockPlaced;
		_world.EarthquakeStartReceived += OnEarthquakeStartReceived;
		_world.KeypadCodeReceived += OnKeypadCodeReceived;
		_world.OpenedEntitiesSnapshotReceived += OnOpenedEntitiesSnapshot;
		_world.BuildingEntityHealthSnapshotReceived += OnBuildingEntityHealthSnapshot;
		_session.RemoteSceneChanged += OnRemoteSceneChanged;
	}

	internal void Unbind()
	{
		_world.BlockDamagedReceived -= _blockBreaks.OnRemoteBlockDamaged;
		_world.BuildingEntityDamagedReceived -= OnRemoteBuildingEntityDamaged;
		_world.BuildingEntityOpenedReceived -= OnRemoteBuildingEntityOpened;
		_world.BlockStateReceived -= OnRemoteBlockState;
		_world.BlockPlacedReceived -= OnRemoteBlockPlaced;
		_world.EarthquakeStartReceived -= OnEarthquakeStartReceived;
		_world.KeypadCodeReceived -= OnKeypadCodeReceived;
		_world.OpenedEntitiesSnapshotReceived -= OnOpenedEntitiesSnapshot;
		_world.BuildingEntityHealthSnapshotReceived -= OnBuildingEntityHealthSnapshot;
		_session.RemoteSceneChanged -= OnRemoteSceneChanged;
	}

	/// <summary>A member (re)entered the world — re-broadcast the keypad codes so
	/// a reconnect gets them immediately instead of waiting up to 60 s for the
	/// periodic cycle (idempotent — an already-set code is left alone).</summary>
	private void OnRemoteSceneChanged(ulong steamId, bool inWorld)
	{
		if (inWorld && IsHostMode && WorldGeneration.world != null) // Unity object — ==
		{
			SendKeypadCodes();
		}
	}

	private bool IsHostMode => _session.Role == SessionRole.Host && _session.SessionActive;

	/// <summary>
	/// Pump: capture the generated baseline once generation completes (host/solo
	/// — the difference table's reference), and periodically re-send the damage
	/// table to in-world members. The lazy Steam P2P session establishes up to
	/// ~30 s after world entry — world-mutation broadcasts sent in that window
	/// are silently dropped (the handshake retries cover the handshake; the
	/// BlockPlaced relay has no retry), so the guest's world keeps the generated
	/// blocks where the host broke them ("the guest's breaks are a subset of the
	/// host's", points appearing right after world entry). The resend is
	/// idempotent (same-value SetBlock) and small (only deviated blocks).
	/// </summary>
	private float _lastSnapshotResend;

	internal void Update()
	{
		if (_session.Role != SessionRole.Guest)
		{
			TryCaptureWorldBaseline();
		}

		if (IsHostMode && _session.SessionActive && Time.unscaledTime - _lastSnapshotResend > 60f)
		{
			_lastSnapshotResend = Time.unscaledTime;
			foreach (var member in _session.Members)
			{
				if (member.InWorld)
				{
					_world.SendBlockStateSnapshot(member.SteamId);
					_world.SendTrapStateSnapshot(member.SteamId); // the one-shot trap consumptions ride the same world-entry resend (idempotent)
					_world.SendOpenedEntitiesSnapshot(member.SteamId); // same for the opened entities (idempotent)
					_world.SendBuildingEntityHealthSnapshot(member.SteamId); // same for the damaged building entities (idempotent)
				}
			}

			SendKeypadCodes(); // re-send the full set (idempotent — set codes are left alone) — covers the lazy-session swallow window and keypads created after the first send (the airdrop/command case, #128 follow-up)
		}
	}

	/// <summary>
	/// Called from the Body.Attack patch after the local attack damaged a
	/// building entity (Body.cs:1946 — the only player-vs-entity damage write,
	/// which otherwise stays local and the peer's copy of the entity never
	/// loses health): report it, position-keyed (world entities are generated
	/// deterministically, so both sides have the same object at the same place).
	/// </summary>
	internal void OnBuildingEntityDamaged(BuildingEntity entity, float damage)
	{
		if (IsRemoteApply || !_session.SessionActive)
		{
			return;
		}

		var pos = entity.transform.position;
		_world.SendBuildingEntityDamaged(new NetVector2(pos.x, pos.y), damage);
		_world.ReportBuildingEntityHealth(pos.x, pos.y, entity.health); // host-only — the late-joiner snapshot's fact source
		_trace.End(_trace.NextOperationId(), 0, "OnBuildingEntityDamaged", "Committed(1)", "EntityDamage");
	}

	/// <summary>
	/// A player's attack damaged a building entity — apply the damage to the
	/// entity at the reported position. A death applied HERE (via this message)
	/// is a REMOTE death: the attacker's side rolls and reports the drops
	/// (local compute — the entity's health is written on both sides, so both
	/// reach zero; only the attacker rolls), so this side is marked with
	/// RemoteEntityDeath and BuildingEntityUpdatePatch suppresses the roll —
	/// it only removes the entity.
	/// </summary>
	private void OnRemoteBuildingEntityDamaged(NetVector2 pos, float damage)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var hit = Physics2D.OverlapPoint(new Vector2(pos.X, pos.Y));
			var entity = hit != null ? hit.GetComponent<BuildingEntity>() : null; // Unity object — ==
			if (entity != null)
			{
				entity.health -= damage;
				if (entity.health < 0.5f)
				{
					entity.gameObject.AddComponent<RemoteEntityDeath>();
				}

				_world.ReportBuildingEntityHealth(pos.X, pos.Y, entity.health); // host-only — a guest-reported hit applied here is part of the authoritative history
			}
			else
			{
				_log.LogWarning("Building entity damage at {Pos} — no entity there (moved or already gone).", pos);
			}
		}
	}

	/// <summary>
	/// Called from the Openable/lockpick/keypad patches after a lockable entity
	/// was opened locally (all three paths write health = 0 directly) — report
	/// it, position-keyed like the entity damage.
	/// </summary>
	internal void OnBuildingEntityOpened(BuildingEntity entity)
	{
		if (IsRemoteApply || !_session.SessionActive)
		{
			return;
		}

		var pos = entity.transform.position;
		_world.SendBuildingEntityOpened(new NetVector2(pos.x, pos.y));
		_world.ReportBuildingEntityHealth(pos.x, pos.y, entity.health); // an open is health = 0 — the snapshot covers it too
		_trace.End(_trace.NextOperationId(), 0, "OnBuildingEntityOpened", "Committed(1)", "Open");
	}

	/// <summary>
	/// A lockable entity was opened — apply the open (health = 0) to the entity
	/// at the reported position. Like the damage path, a death applied here is
	/// REMOTE: the opener's side rolls and reports the drops, this side is
	/// marked and BuildingEntityUpdatePatch only removes the entity.
	/// </summary>
	private void OnRemoteBuildingEntityOpened(NetVector2 pos)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var hit = Physics2D.OverlapPoint(new Vector2(pos.X, pos.Y));
			var entity = hit != null ? hit.GetComponent<BuildingEntity>() : null; // Unity object — ==
			if (entity != null)
			{
				entity.health = 0f;
				entity.gameObject.AddComponent<RemoteEntityDeath>();
				_world.ReportBuildingEntityHealth(pos.X, pos.Y, 0f); // host-only — the opened state is part of the late-joiner history
			}
			else
			{
				_log.LogWarning("Building entity open at {Pos} — no entity there (moved or already gone).", pos);
			}
		}
	}

	/// <summary>
	/// The host's opened-entities snapshot arrived (world entry / the 60 s
	/// resend) — apply every open through the SAME application as the live
	/// relay (health = 0 + the remote-death mark). Idempotent by construction:
	/// an already-open entity's health is 0 again.
	/// </summary>
	private void OnOpenedEntitiesSnapshot(IReadOnlyList<NetVector2Msg> positions)
	{
		foreach (var pos in positions)
		{
			OnRemoteBuildingEntityOpened(new NetVector2(pos.X, pos.Y));
		}

		_log.LogInformation("Opened-entities snapshot applied ({Count} positions).", positions.Count);
	}

	/// <summary>
	/// The host's building-entity health snapshot arrived (world entry / the
	/// 60 s resend) — apply every entry through the SAME semantic as the live
	/// relay: write the host's current health, and mark a death applied here as
	/// remote so this side never rolls a second set of drops. Idempotent by
	/// construction: writing the same health again is a no-op.
	/// </summary>
	private void OnBuildingEntityHealthSnapshot(IReadOnlyList<BuildingEntityHealthEntryMsg> entries)
	{
		foreach (var entry in entries)
		{
			ApplyRemoteBuildingEntityHealth(entry.X, entry.Y, entry.Health);
		}

		_log.LogInformation("Building-entity health snapshot applied ({Count} entities).", entries.Count);
	}

	private void ApplyRemoteBuildingEntityHealth(float x, float y, float health)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var hit = Physics2D.OverlapPoint(new Vector2(x, y));
			var entity = hit != null ? hit.GetComponent<BuildingEntity>() : null; // Unity object — ==
			if (entity != null)
			{
				entity.health = health;
				if (entity.health < 0.5f)
				{
					entity.gameObject.AddComponent<RemoteEntityDeath>();
				}
			}
			else
			{
				_log.LogWarning("Building-entity health snapshot at ({X}, {Y}) — no entity there (moved or already gone).", x, y);
			}
		}
	}

	/// <summary>
	/// Called from the SetBlock patch after any world mutation (mining,
	/// placement, EARTHQUAKES, remote application). Host/solo: diff against the
	/// generated baseline (equal → removed from the difference table, otherwise
	/// upserted) and broadcast the mutation live — air writes included: the
	/// earthquake (WorldGeneration.cs:895) and environment breaks SetBlock(0)
	/// on each side with INDEPENDENT random, so without the air-write relay the
	/// two sides' terrain diverges ("the item keeps being pulled back" — items
	/// fall through holes that exist on one side only). Guest: report local
	/// mutations for arbitration (mining double-reports via BlockDamaged —
	/// idempotent). Remote applications are guarded (they answer their own
	/// way); generation-time SetBlock calls are the baseline itself and excluded.
	/// </summary>
	internal void OnBlockSet(Vector2Int pos, ushort block)
	{
		if (IsRemoteApply || HarmonyTraverse.IsGenerating())
		{
			return;
		}

		// Trace only the PLAYER-driven writes: mining and placement (the postfix
		// verified the write landed — GetBlock == block). Quake/environment
		// breaks fire inside WorldGeneration.Update at 16/s per side — the
		// [Earthquake] summary lines cover those; a per-break trace would drown
		// the log.
		if (_session.SessionActive && !WorldGenerationUpdatePatch.InUpdate)
		{
			_trace.End(_trace.NextOperationId(), 0, "OnBlockSet", "Committed(1)", "BlockSet");
		}

		// Host OR solo: diff against the generated baseline (equal → removed
		// from the difference table, otherwise upserted). Solo tracking is
		// what lets a solo game that opens a lobby later hand its accumulated
		// world changes to a joining guest (the guest regenerates the seed
		// world and applies the table). Guests do not track — they only apply.
		if (_session.Role != SessionRole.Guest)
		{
			if (_baseline is null)
			{
				TryCaptureWorldBaseline(); // generation may have just completed this frame
				if (_baseline is null)
				{
					return; // still no baseline — nothing to diff against
				}
			}

			if (block == _baseline[pos.x, pos.y])
			{
				_world.RemoveBlockState(pos.x, pos.y); // restored to baseline — no longer a difference
			}
			else
			{
				_world.ReportBlockState(pos.x, pos.y, block);
			}
		}

		if (_session.SessionActive)
		{
			// A world mutation in a live session: the source applied it locally
			// (local compute) — host broadcasts it, guest reports it for
			// arbitration. Solo (no session) never sends.
			if (_session.Role == SessionRole.Host)
			{
				_world.BroadcastBlockPlaced(0, pos.x, pos.y, block);
			}
			else if (_session.Role == SessionRole.Guest)
			{
				_world.SendBlockPlacedReport(pos.x, pos.y, block);
			}
		}
	}

	/// <summary>
	/// A mutation arrived: host arbitrates — a PLACEMENT (block != 0) must land
	/// on air (the game's own placement condition, Item.cs), an AIR write
	/// (earthquake/environment break, block == 0) must land on something — then
	/// applies, records the difference and relays (source excluded); guest
	/// applies it directly. An APPLIED air write also records the sender's
	/// break for the drops arbitration: when that sender's BlockDamaged report
	/// (the drops carrier) arrives later, the record proves the break was the
	/// first writer (see _recentBroken).
	/// </summary>
	private void OnRemoteBlockPlaced(ulong sender, int x, int y, ushort block)
	{
		if (WorldGeneration.world == null) // Unity object — ==
		{
			return;
		}

		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var pos = new Vector2Int(x, y);
			if (IsHostMode)
			{
				if ((block == 0) == (WorldGeneration.world.GetBlock(pos) == 0))
				{
					// Placement onto occupied / break of air — first-writer-wins,
					// no relay (the two sides' independent earthquakes racing the
					// same spot: one wins, both apply the winner via the relay).
					return;
				}

				WorldGeneration.world.SetBlock(pos, block);
				_world.ReportBlockState(x, y, block); // the mutation is a world difference too
				_world.BroadcastBlockPlaced(sender, x, y, block); // the reporter already applied locally
				if (block == 0)
				{
					// A player break (its BlockDamaged report follows) — or a
					// quake/environment write (expires unused in BlockBreakSync).
					_blockBreaks.OnRemoteAirWriteApplied(sender, pos);
				}
			}
			else
			{
				WorldGeneration.world.SetBlock(pos, block);
			}
		}
	}

	/// <summary>
	/// An earthquake just started (detected in WorldGenerationUpdatePatch). The
	/// HOST broadcasts it (quake timing is synced to the host: guests show the
	/// effect and re-align their timer, so every side shakes together and
	/// breaks its own nearby region; the regions union via the air-write relay,
	/// overlaps count once). A GUEST never starts one: its timer is frozen by
	/// the patch's Prefix (WorldGenerationUpdatePatch), so a start observed
	/// here is either the host's broadcast landing mid-frame (frame order) or
	/// a freeze leak — never canceled (canceling a broadcast-driven quake is
	/// "started then ended"; the freeze is the guard). Solo play (no session)
	/// quakes normally.
	/// </summary>
	internal void OnEarthquakeStarted(float duration, float nextDelay)
	{
		if (IsHostMode && _session.SessionActive)
		{
			_log.LogInformation("[Earthquake] host quake started ({Duration:F1}s, next in {NextDelay:F0}s) — broadcasting.", duration, nextDelay);
			_world.BroadcastEarthquakeStart(duration, nextDelay);
		}
		else if (_session.SessionActive)
		{
			_log.LogInformation("[Earthquake] guest quake start observed ({Duration:F1}s) — timer frozen, host broadcast drives it.", duration);
		}
		else
		{
			_log.LogInformation("[Earthquake] local quake started on this side ({Duration:F1}s, next in {NextDelay:F0}s) — solo, no session.", duration, nextDelay);
		}
	}

	/// <summary>Guest side: an earthquake began (host timing) — show the effect (earthquakeTime drives the Update intensity ramp) and re-align the local quake timer to the host's next delay, so the next quake fires on all sides together.</summary>
	private void OnEarthquakeStartReceived(float duration, float nextDelay)
	{
		if (WorldGeneration.world == null) // Unity object — ==
		{
			return;
		}

		WorldGeneration.world.earthquakeTime = duration;
		WorldGeneration.world.earthquakeDelay = nextDelay;
		_log.LogInformation("[Earthquake] guest: host quake ({Duration:F1}s) — showing effect, timer re-aligned ({NextDelay:F0}s).", duration, nextDelay);
	}

	/// <summary>
	/// Host only: snapshot worldBlocks the moment generation completes (the
	/// generated baseline the difference table diffs against). Any generation
	/// start resets the flag; a completed generation re-captures — per
	/// world/layer, matching the table reset at CaptureWorldParams.
	/// </summary>
	private void TryCaptureWorldBaseline()
	{
		var world = WorldGeneration.world;
		if (world == null || HarmonyTraverse.IsGenerating()) // Unity object — ==
		{
			_baseline = null;
			return;
		}

		if (_baseline is not null)
		{
			return; // already captured for this generation
		}

		var blocks = HarmonyTraverse.ReadWorldBlocks(world);
		if (blocks is null)
		{
			return;
		}

		_baseline = (ushort[,])blocks.Clone();
		_world.ResetDamagedBlocks();
		_log.LogInformation("Captured world baseline ({Width}x{Height}) — the damage table now diffs against it.",
			_baseline.GetLength(0), _baseline.GetLength(1));

		if (IsHostMode)
		{
			SendKeypadCodes(); // TryCaptureWorldBaseline runs once per generation — the first send happens here, the 60 s cycle re-sends
		}
	}

	/// <summary>
	/// Host only: generate every keypad's code (the game lazy-generates on first
	/// use per side, Openable.cs:19 — every side would get its own code) and
	/// broadcast them position-keyed. Runs at world entry (after the generation
	/// completed — the Openables exist by then) and re-runs on the 60 s cycle:
	/// the re-send covers the lazy Steam P2P session's swallow window and
	/// keypads created after the first send (the airdrop/command case, #128
	/// follow-up — created keypads are broadcast immediately by
	/// <see cref="OnEntityInstantiated"/>, this is the fallback). Idempotent:
	/// the receiver leaves an already-set code alone.
	/// </summary>
	private void SendKeypadCodes()
	{
		var codes = new List<KeypadEntryMsg>();
		foreach (var openable in UnityEngine.Object.FindObjectsOfType<Openable>())
		{
			if (!openable.isKeypad)
			{
				continue;
			}

			var pos = openable.transform.position;
			codes.Add(new KeypadEntryMsg
			{
				Position = new NetVector2(pos.x, pos.y).ToNetVector2Msg(),
				Code = EnsureKeypadCode(openable),
			});
		}

		if (codes.Count > 0)
		{
			_world.SendKeypadCodes(codes);
		}
	}

	/// <summary>Read the Openable's code, generating it host-side if unset (the
	/// host's Random stream decides — same authority as the game's lazy
	/// generation). Internal: the runtime-creation channel (EntitySpawnSync)
	/// generates a created keypad's code at relay time and carries it in the
	/// EntitySpawnedMsg (#128 — one message per operation; the code is
	/// creation-time data, the game lazy-generates it per side otherwise).</summary>
	internal static string EnsureKeypadCode(Openable openable)
	{
		var codeField = Traverse.Create(openable).Field("code");
		var existing = codeField.GetValue<string>();
		if (string.IsNullOrEmpty(existing))
		{
			existing = KeypadMinigame.GenerateCode(); // host authority — its Random stream decides
			codeField.SetValue(existing);
		}

		return existing;
	}

	/// <summary>
	/// Guest side: the host's keypad codes arrived — write them onto the local
	/// Openables (position-keyed: deterministic world entities sit at the same
	/// place on both sides). A code already set (a local first use raced the
	/// broadcast) is left alone.
	/// </summary>
	private void OnKeypadCodeReceived(IReadOnlyList<KeypadEntryMsg> codes)
	{
		if (codes.Count == 0)
		{
			return;
		}

		var applied = 0;
		foreach (var openable in UnityEngine.Object.FindObjectsOfType<Openable>())
		{
			if (!openable.isKeypad)
			{
				continue;
			}

			var pos = openable.transform.position;
			var match = codes.FirstOrDefault(c =>
				Vector2.Distance(new Vector2(c.Position.X, c.Position.Y), new Vector2(pos.x, pos.y)) < 3f);
			if (match is null)
			{
				continue;
			}

			var codeField = Traverse.Create(openable).Field("code");
			if (string.IsNullOrEmpty(codeField.GetValue<string>()))
			{
				codeField.SetValue(match.Code);
				applied++;
			}
		}

		_log.LogInformation("[Keypad] applied {Applied} host keypad code(s).", applied);
	}

	/// <summary>
	/// Guest side: the host's authoritative block-state snapshot — apply the
	/// accumulated mutations to our freshly generated world (the snapshot only
	/// arrives after our InWorld report, i.e. after generation finished).
	/// </summary>
	private void OnRemoteBlockState(IReadOnlyList<DamagedBlock> blocks)
	{
		if (WorldGeneration.world == null || HarmonyTraverse.IsGenerating()) // Unity object — ==
		{
			return;
		}

		foreach (var block in blocks)
		{
			WorldGeneration.world.SetBlock(new Vector2Int(block.X, block.Y), block.Block);
		}

		_log.LogInformation("Applied host block-state snapshot ({Count} blocks).", blocks.Count);
	}
}
