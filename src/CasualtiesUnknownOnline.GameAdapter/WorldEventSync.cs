using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.GameAdapter.Items;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// World-event domain: block damage/placement, building-entity damage/open,
/// earthquakes (host timing) and the generated-baseline difference table —
/// local compute → report → host relay/arbitration, position-keyed. Owns the
/// deferred spawn-landing presentation (sound + camera shake) that the start
/// gate must not play into the frozen world.
/// </summary>
internal sealed class WorldEventSync(
	SessionService session,
	WorldService world,
	ILogger<WorldEventSync> log)
{
	private readonly SessionService _session = session;
	private readonly WorldService _world = world;
	private readonly ILogger<WorldEventSync> _log = log;

	/// <summary>Reentry guards: remote applications must not echo back as local reports.</summary>
	private bool _applyingRemoteBlockDamage;
	private bool _applyingRemoteBlockPlace;
	private bool _applyingRemoteBuildingDamage;

	/// <summary>The generated world snapshot the difference table diffs against (host/solo only).</summary>
	private ushort[,]? _baseline;

	/// <summary>Host: the keypad codes were generated + broadcast once per world entry.</summary>
	private bool _keypadCodesSent;

	internal void BindToSession()
	{
		_world.BlockDamagedReceived += OnRemoteBlockDamaged;
		_world.BuildingEntityDamagedReceived += OnRemoteBuildingEntityDamaged;
		_world.BuildingEntityOpenedReceived += OnRemoteBuildingEntityOpened;
		_world.BlockStateReceived += OnRemoteBlockState;
		_world.BlockPlacedReceived += OnRemoteBlockPlaced;
		_world.EarthquakeStartReceived += OnEarthquakeStartReceived;
		_world.KeypadCodeReceived += OnKeypadCodeReceived;
	}

	internal void Unbind()
	{
		_world.BlockDamagedReceived -= OnRemoteBlockDamaged;
		_world.BuildingEntityDamagedReceived -= OnRemoteBuildingEntityDamaged;
		_world.BuildingEntityOpenedReceived -= OnRemoteBuildingEntityOpened;
		_world.BlockStateReceived -= OnRemoteBlockState;
		_world.BlockPlacedReceived -= OnRemoteBlockPlaced;
		_world.EarthquakeStartReceived -= OnEarthquakeStartReceived;
		_world.KeypadCodeReceived -= OnKeypadCodeReceived;
	}

	private bool IsHostMode => _session.Role == SessionRole.Host && _session.SessionActive;

	/// <summary>Pump: capture the generated baseline once generation completes (host/solo — the difference table's reference).</summary>
	internal void Update()
	{
		if (_session.Role != SessionRole.Guest)
		{
			TryCaptureWorldBaseline();
		}
	}

	/// <summary>
	/// Called from the DamageBlock patch after a LOCAL block damage was applied:
	/// report it so the peer applies the same damage at the same world position.
	/// </summary>
	internal void OnBlockDamaged(Vector2 pos, float dmg)
	{
		if (_applyingRemoteBlockDamage || !_session.SessionActive)
		{
			return;
		}

		_world.SendBlockDamaged(new NetVector2(pos.x, pos.y), dmg);
	}

	/// <summary>
	/// The peer damaged a block — apply it locally (remote verify/sync). Drops
	/// are host-authoritative: the HOST's application rolls them (ignoreLoot=
	/// false here on the host — its own Random stream decides what falls, and
	/// the items broadcast through the item domain); the guest's application
	/// never rolls (ignoreLoot=true — its local attacks are also force-no-roll
	/// by WorldGenerationDamageBlockLootPatch). Rolling only on the host keeps
	/// the dropped items' spawn state (what/where/velocity) identical on both
	/// sides — independent rolls would draw from each side's own Random stream.
	/// </summary>
	private void OnRemoteBlockDamaged(NetVector2 pos, float dmg)
	{
		if (WorldGeneration.world == null) // Unity object — ==
		{
			return;
		}

		_applyingRemoteBlockDamage = true;
		try
		{
			WorldGeneration.world.DamageBlock(
				WorldGeneration.world.WorldToBlockPos(new Vector2(pos.X, pos.Y)), dmg, true, false,
				_session.Role != SessionRole.Host);
		}
		finally
		{
			_applyingRemoteBlockDamage = false;
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
		if (_applyingRemoteBuildingDamage || !_session.SessionActive)
		{
			return;
		}

		var pos = entity.transform.position;
		_world.SendBuildingEntityDamaged(new NetVector2(pos.x, pos.y), damage);
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
		_applyingRemoteBuildingDamage = true;
		try
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
			}
			else
			{
				_log.LogWarning("Building entity damage at {Pos} — no entity there (moved or already gone).", pos);
			}
		}
		finally
		{
			_applyingRemoteBuildingDamage = false;
		}
	}

	/// <summary>
	/// Called from the Openable/lockpick/keypad patches after a lockable entity
	/// was opened locally (all three paths write health = 0 directly) — report
	/// it, position-keyed like the entity damage.
	/// </summary>
	internal void OnBuildingEntityOpened(BuildingEntity entity)
	{
		if (_applyingRemoteBuildingDamage || !_session.SessionActive)
		{
			return;
		}

		var pos = entity.transform.position;
		_world.SendBuildingEntityOpened(new NetVector2(pos.x, pos.y));
	}

	/// <summary>
	/// A lockable entity was opened — apply the open (health = 0) to the entity
	/// at the reported position. Like the damage path, a death applied here is
	/// REMOTE: the opener's side rolls and reports the drops, this side is
	/// marked and BuildingEntityUpdatePatch only removes the entity.
	/// </summary>
	private void OnRemoteBuildingEntityOpened(NetVector2 pos)
	{
		_applyingRemoteBuildingDamage = true;
		try
		{
			var hit = Physics2D.OverlapPoint(new Vector2(pos.X, pos.Y));
			var entity = hit != null ? hit.GetComponent<BuildingEntity>() : null; // Unity object — ==
			if (entity != null)
			{
				entity.health = 0f;
				entity.gameObject.AddComponent<RemoteEntityDeath>();
			}
			else
			{
				_log.LogWarning("Building entity open at {Pos} — no entity there (moved or already gone).", pos);
			}
		}
		finally
		{
			_applyingRemoteBuildingDamage = false;
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
		if (_applyingRemoteBlockDamage || _applyingRemoteBlockPlace || HarmonyTraverse.IsGenerating())
		{
			return;
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
	/// applies it directly.
	/// </summary>
	private void OnRemoteBlockPlaced(ulong sender, int x, int y, ushort block)
	{
		if (WorldGeneration.world == null) // Unity object — ==
		{
			return;
		}

		_applyingRemoteBlockPlace = true;
		try
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
			}
			else
			{
				WorldGeneration.world.SetBlock(pos, block);
			}
		}
		finally
		{
			_applyingRemoteBlockPlace = false;
		}
	}

	/// <summary>An earthquake just started (detected in WorldGenerationUpdatePatch) — the HOST broadcasts it (quake timing is synced to the host: guests show the effect and re-align their timer, so every side shakes together and breaks its own nearby region; the regions union via the air-write relay, overlaps count once).</summary>
	internal void OnEarthquakeStarted(float duration, float nextDelay)
	{
		if (IsHostMode && _session.SessionActive)
		{
			_log.LogInformation("[Earthquake] host quake started ({Duration:F1}s, next in {NextDelay:F0}s) — broadcasting.", duration, nextDelay);
			_world.BroadcastEarthquakeStart(duration, nextDelay);
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

		if (IsHostMode && !_keypadCodesSent)
		{
			_keypadCodesSent = true;
			SendKeypadCodesOnce();
		}
	}

	/// <summary>
	/// Host only: generate every keypad's code once (the game lazy-generates on
	/// first use per side, Openable.cs:19 — every side would get its own code)
	/// and broadcast them position-keyed. Runs at world entry, after the
	/// generation completed (the Openables exist by then).
	/// </summary>
	private void SendKeypadCodesOnce()
	{
		var codes = new List<KeypadEntryMsg>();
		foreach (var openable in UnityEngine.Object.FindObjectsOfType<Openable>())
		{
			if (!openable.isKeypad)
			{
				continue;
			}

			var codeField = Traverse.Create(openable).Field("code");
			var existing = codeField.GetValue<string>();
			if (string.IsNullOrEmpty(existing))
			{
				existing = KeypadMinigame.GenerateCode(); // host authority — its Random stream decides
				codeField.SetValue(existing);
			}

			var pos = openable.transform.position;
			codes.Add(new KeypadEntryMsg
			{
				Position = new NetVector2(pos.x, pos.y).ToNetVector2Msg(),
				Code = existing,
			});
		}

		if (codes.Count > 0)
		{
			_world.SendKeypadCodes(codes);
		}

		_log.LogInformation("[Keypad] generated {Count} keypad code(s) — broadcasting.", codes.Count);
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
