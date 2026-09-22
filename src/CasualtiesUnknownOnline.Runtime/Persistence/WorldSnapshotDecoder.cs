using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;
using CasualtiesUnknownOnline.Application.Kernel;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// The disk → in-memory half of §3.4, and the place §6's per-entry salvage
/// actually happens: the reader hands this decoder ONE entry at a time, so an
/// entry whose content no longer exists (a prefab or item definition a mod
/// update removed, an unmappable id) is skipped by itself while the rest of its
/// domain still applies. Decoding never throws out of this class.
///
/// A snapshot whose run baseline cannot be read is REFUSED as a whole: the layer
/// it restores into would otherwise be guessed, and §6 forbids silently
/// regenerating a layer.
/// </summary>
public sealed class WorldSnapshotDecoder(SaveManifest manifest, ILogger<WorldSnapshotDecoder> log)
{
	private readonly SaveManifest _manifest = manifest;
	private readonly ILogger<WorldSnapshotDecoder> _log = log;

	// The accumulated KERNEL facts, not the wire DTOs: the wire→kernel mapping is
	// the step that can reject an entry (an unknown item location kind, a prefab
	// the content set no longer has), so it runs per entry inside this class's
	// catch — never in Finish(), where one bad row would take the whole domain
	// down with it (§6).
	private readonly List<ItemState> _items = [];
	private readonly List<PlayerState> _players = [];
	private readonly List<EnemyState> _enemies = [];
	private readonly List<EntityId> _removedEnemies = [];
	private readonly List<FluidRegionState> _fluids = [];
	private readonly List<SavedCharacter> _characters = [];
	private readonly List<SaveWorldBlockRow> _worldBlocks = [];
	private readonly List<SaveWorldTransientRow> _worldTransients = [];

	private WorldEntityState _worldEntities = WorldEntityState.Empty;
	private RunState? _run;
	private SaveNativeRunFields? _nativeRunFields;
	private string _currentPath = string.Empty;
	private int _entryIndex;

	/// <summary>The decode callback for <see cref="SaveArchiveReader.ReadSalvage"/>; one call per entry of every payload file.</summary>
	public void DecodeEntry(JsonElement entry, SalvageSession session)
	{
		var path = session.CurrentPath;
		if (!string.Equals(path, _currentPath, StringComparison.Ordinal))
		{
			_currentPath = path;
			_entryIndex = 0;
		}

		var id = IdOf(entry, _entryIndex++);
		if (SaveArchiveFormat.IsCharacterPath(path))
		{
			DecodeCharacter(entry, session, SaveArchiveFormat.PlayerKeyOfCharacterPath(path), id);
			return;
		}

		switch (path)
		{
			case SaveArchiveFormat.RunFileName:
				DecodeRun(entry, session, id);
				return;
			case SaveArchiveFormat.PlayersFileName:
				AddMapped<WirePlayerState, PlayerState>(_players, entry, session, id, KernelDomainWireMapper.FromWirePlayerState);
				return;
			case SaveArchiveFormat.ItemsFileName:
				AddMapped<WireItem, ItemState>(_items, entry, session, id, KernelWireMapper.FromWireItem);
				return;
			case SaveArchiveFormat.EnemiesFileName:
				DecodeEnemyRow(entry, session, id);
				return;
			case SaveArchiveFormat.FluidsFileName:
				AddMapped<WireFluidRegionState, FluidRegionState>(_fluids, entry, session, id, KernelDomainWireMapper.FromWireFluidRegionState);
				return;
			case SaveArchiveFormat.WorldEntitiesFileName:
				DecodeWorldEntityRow(entry, session, id);
				return;
			case SaveArchiveFormat.WorldBlocksFileName:
				DecodeWorldBlockRow(entry, session, id);
				return;
			case SaveArchiveFormat.WorldTransientsFileName:
				DecodeWorldTransientRow(entry, session, id);
				return;
			default:
				session.Skip(id, "not a domain file of this format", path);
				return;
		}
	}

	/// <summary>The decode verdict after the reader finished walking the snapshot's files.</summary>
	public WorldSnapshotDecode Finish()
	{
		if (_run is null)
		{
			return WorldSnapshotDecode.Refused("the snapshot has no readable run baseline (run.json)");
		}

		if (!TryReadEpoch(out var epoch))
		{
			return WorldSnapshotDecode.Refused($"the manifest's run epoch '{_manifest.RunEpoch}' is not a run epoch");
		}

		if (_manifest.Kind == WorldCutKind.LayerEnd && (_worldBlocks.Count > 0 || _worldTransients.Count > 0))
		{
			// The manifest names what the snapshot IS. A layer-end cut records no
			// in-layer fact — the layer it names is regenerated from the run baseline
			// (§3.4/§4) — so a layer-end snapshot that carries facts contradicts
			// itself: applying them would graft one layer's mutations onto a
			// regenerated layer, and dropping them quietly is what §6 forbids. The
			// whole snapshot is refused instead.
			return WorldSnapshotDecode.Refused(
				$"the manifest names a layer-end cut, which records no in-layer fact, but the snapshot carries {_worldBlocks.Count} world-block row(s) and {_worldTransients.Count} transient row(s)");
		}

		if (epoch != _run.RunId)
		{
			// Not a refusal: the kernel accepts a run whose id differs from the
			// authority epoch, and the manifest's epoch is what a late-joining guest
			// adopts from the wire checkpoint. It IS provenance drift worth naming —
			// the two are equal for every cut this build writes.
			_log.LogWarning("Snapshot of world {WorldId}: the manifest's run epoch {Epoch} differs from the run baseline's run id {RunId}.",
				_manifest.WorldId, epoch, _run.RunId);
		}

		var checkpoint = new GameCheckpoint(
			new RunEpoch(epoch),
			(ulong)_manifest.GlobalRevision,
			_items,
			null,
			_run,
			_worldEntities,
			_players.Count == 0 ? null : new PlayerStateTable(_players),
			_enemies.Count == 0 && _removedEnemies.Count == 0
				? null
				: new EnemyStateTable(_enemies, _removedEnemies),
			_fluids.Count == 0 ? null : new FluidStateTable(_fluids));

		// Debug, not Information: the restore that drives this decoder logs ONE account
		// line per restore carrying these same per-domain counts (S4 scope 3), and a
		// second Information line for the same facts is the noise that hides it.
		_log.LogDebug("Decoded snapshot of world {WorldId}: epoch {Epoch}, revision {Revision}, {Items} item(s), {Players} player(s), {Enemies} enemy(ies), {Characters} character(s), {Blocks} world block(s), {Transients} transient(s), native run fields {RunFields}.",
			_manifest.WorldId, epoch, (ulong)_manifest.GlobalRevision, _items.Count, _players.Count, _enemies.Count + _removedEnemies.Count, _characters.Count, _worldBlocks.Count, _worldTransients.Count,
			_nativeRunFields is null ? "absent" : $"present ({_nativeRunFields.Recipes.Count} recipe row(s), clock {_nativeRunFields.SavedRunTime:F1}, layer time {(_nativeRunFields.LayerTimeSpent is { } spent ? spent.ToString("F1") : "absent")})");
		return new WorldSnapshotDecode(checkpoint, _characters, null, _worldBlocks, _worldTransients, _nativeRunFields);
	}

	/// <summary>
	/// One row of <c>run.json</c>. The kernel baseline is what makes the snapshot
	/// usable at all — without it the layer cannot be reproduced, which is a
	/// refusal rather than a salvage. The native row (the run clock base and the
	/// recipe unlock table) is optional by construction: a snapshot written before
	/// this row existed does not carry it, and the restore NAMES that instead of
	/// letting the world continue with a clock that restarts at zero and every
	/// recipe re-locked (§6). A malformed native row is skipped by itself, which
	/// leaves exactly the same named gap.
	/// </summary>
	private void DecodeRun(JsonElement entry, SalvageSession session, string id)
	{
		var row = Decode<SaveRunRow>(entry, session, id);
		if (row is null || !CarriesItsOwnPayload(entry, row))
		{
			if (row is not null)
			{
				// A row with NO kind is not "an unknown kind" — it is an archive
				// written before run.json carried typed rows, and saying so is what
				// lets a reader tell a format change from corruption.
				session.Skip(
					row.Describe(),
					string.IsNullOrEmpty(row.Kind)
						? "this run row declares no kind; run.json carries typed rows (`run` and `native-run-fields`), so an archive written before that shape cannot be read"
						: $"a run row of kind '{row.Kind}' does not carry that kind's payload",
					_currentPath);
			}

			return;
		}

		switch (row.Kind)
		{
			case SaveRunRow.RunKind:
				if (_run is not null)
				{
					session.Skip(id, "a snapshot carries exactly one run baseline", _currentPath);
					return;
				}

				if (row.Run!.RandomState is not { Length: > 0 })
				{
					// Without the generation baseline the layer cannot be reproduced:
					// that is a refusal, not a salvage (a fresh layer would be a
					// silent restart).
					session.Skip(id, "the run baseline carries no generation random state", _currentPath);
					return;
				}

				_run = KernelDomainWireMapper.FromWireRun(row.Run);
				return;
			case SaveRunRow.NativeRunFieldsKind:
				if (_nativeRunFields is not null)
				{
					session.Skip(id, "a snapshot carries exactly one native run-field row", _currentPath);
					return;
				}

				_nativeRunFields = new SaveNativeRunFields
				{
					SavedRunTime = row.NativeRunFields!.SavedRunTime,
					Recipes = row.NativeRunFields.Recipes,
					// Absence is NOT read as zero: a cut that carried no layer timer (a
					// layer-end cut, or an archive written before the value existed) leaves
					// the live timer alone, and the restore names that.
					LayerTimeSpent = entry.GetProperty("nativeRunFields").TryGetProperty("layerTimeSpent", out var layerTime) && layerTime.ValueKind != JsonValueKind.Null
						? layerTime.GetSingle()
						: null,
				};
				return;
			default:
				session.Skip(
					row.Describe(),
					string.IsNullOrEmpty(row.Kind)
						? "this run row declares no kind; run.json carries typed rows (`run` and `native-run-fields`) and an archive written before that shape cannot be read"
						: $"a run row declares an unusable kind '{row.Kind}'",
					_currentPath);
				return;
		}
	}

	private void DecodeCharacter(JsonElement entry, SalvageSession session, string playerKey, string id)
	{
		var character = Decode<CharacterDataMsg>(entry, session, $"{playerKey}/{id}");
		if (character is not null)
		{
			_characters.Add(new SavedCharacter(playerKey, character));
		}
	}

	private void DecodeEnemyRow(JsonElement entry, SalvageSession session, string id)
	{
		var row = Decode<SaveEnemyRow>(entry, session, id);
		if (row is null)
		{
			return;
		}

		switch (row.Kind)
		{
			case SaveEnemyRow.RemovedKind when row.Removed is not null:
				AddMapped(_removedEnemies, entry, session, id, KernelDomainWireMapper.FromWireEntityId, row, row.Removed);
				return;
			case SaveEnemyRow.EnemyKind when row.Enemy is not null:
				AddMapped(_enemies, entry, session, id, KernelDomainWireMapper.FromWireEnemyState, row, row.Enemy);
				return;
			default:
				session.Skip(row.Describe(), $"an enemy row declares an unusable kind '{row.Kind}'", _currentPath);
				return;
		}
	}

	/// <summary>
	/// One row of the world-entity table. The accumulator is replaced only after
	/// the whole row decoded, so a row the mapping rejects leaves the table as it
	/// was (§6: the remaining facts still apply).
	/// </summary>
	private void DecodeWorldEntityRow(JsonElement entry, SalvageSession session, string id)
	{
		var row = Decode<SaveWorldEntityRow>(entry, session, id);
		if (row is null)
		{
			return;
		}

		try
		{
			// The switch computes the NEXT table; it is assigned only when the row
			// materialized, so a rejected row leaves the facts already applied alone.
			_worldEntities = row.Kind switch
			{
				SaveWorldEntityRow.TrapConsumptionKind when row.TrapConsumption is not null =>
					_worldEntities.WithConsumption(KernelDomainWireMapper.FromWireTrapConsumption(row.TrapConsumption)),
				SaveWorldEntityRow.BuildingHealthKind when row.BuildingHealth is not null =>
					_worldEntities.WithBuildingHealth(KernelDomainWireMapper.FromWireBuildingEntityHealth(row.BuildingHealth)),
				SaveWorldEntityRow.OpenedEntityKind when row.OpenedEntity is not null =>
					_worldEntities.WithOpened(KernelDomainWireMapper.FromWireOpenedEntity(row.OpenedEntity)),
				SaveWorldEntityRow.TrapStateKind when row.TrapState is not null =>
					_worldEntities.WithTrapState(KernelDomainWireMapper.FromWireTrapStateFact(row.TrapState)),
				_ => throw new InvalidOperationException($"the row declares kind '{row.Kind}' but carries no such fact"),
			};
		}
		catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
		{
			session.Skip(row.Describe(), "the world-entity row could not be materialized", ex.Message);
		}
	}

	/// <summary>
	/// One row of the block diff. Unlike the world-entity table there is no
	/// wire→kernel conversion that could reject a readable row: the block cell and
	/// the block id ARE the restored shape, so the row is kept when it carries the
	/// payload of the kind it declares, and a row that does not is skipped by
	/// itself — the rest of the file still applies (§6).
	/// </summary>
	private void DecodeWorldBlockRow(JsonElement entry, SalvageSession session, string id)
	{
		var row = Decode<SaveWorldBlockRow>(entry, session, id);
		if (row is null)
		{
			return;
		}

		if (CarriesItsOwnPayload(entry, row))
		{
			_worldBlocks.Add(row);
			return;
		}

		session.Skip(DescribeOr(row.Describe(), id), $"a world-block row declares an unusable kind '{row.Kind}'", _currentPath);
	}

	/// <summary>One row of the transient world facts, salvaged on its own exactly like a block row.</summary>
	private void DecodeWorldTransientRow(JsonElement entry, SalvageSession session, string id)
	{
		var row = Decode<SaveWorldTransientRow>(entry, session, id);
		if (row is null)
		{
			return;
		}

		if (CarriesItsOwnPayload(entry, row))
		{
			_worldTransients.Add(row);
			return;
		}

		session.Skip(DescribeOr(row.Describe(), id), $"a world-transient row declares an unusable kind '{row.Kind}'", _currentPath);
	}

	/// <summary>
	/// The row's own identity when it has one, the reader's entry id otherwise: a row
	/// whose kind or cell is missing describes itself as <c>&lt;…&gt;</c>, and a
	/// report entry with no id at all would not say WHICH row was dropped (§6).
	/// </summary>
	private static string DescribeOr(string described, string id) =>
		described.StartsWith("<", StringComparison.Ordinal) && described.EndsWith(">", StringComparison.Ordinal) ? id : described;

	/// <summary>
	/// True = the row carries a COMPLETE payload for the kind it declares. A kind
	/// this build does not know is NOT an error (a newer writer's row, §6.1), so it
	/// answers false and is skipped. A known kind whose payload is missing, is not
	/// an object, or omits a field is malformed: its fields would deserialize to
	/// type defaults (block 0 at (0,0), a radiation line at 0/0) and those defaults
	/// would be written onto the world as if they had been recorded — the field
	/// NAMES are therefore read off the raw JSON, where absent and zero are
	/// distinguishable.
	/// </summary>
	private static bool CarriesItsOwnPayload(JsonElement entry, SaveWorldBlockRow row) => row.Kind switch
	{
		SaveWorldBlockRow.BlockStateKind => HasAllProperties(entry, "blockState", "x", "y", "block"),
		SaveWorldBlockRow.NativeBlockDamageKind => HasAllProperties(entry, "nativeBlockDamage", "x", "y", "damage"),
		_ => false,
	};

	private static bool CarriesItsOwnPayload(JsonElement entry, SaveWorldTransientRow row) => row.Kind switch
	{
		SaveWorldTransientRow.KeypadKind => HasPosition(entry, "keypad") && HasAllProperties(entry, "keypad", "code"),
		SaveWorldTransientRow.GeyserKind => HasPosition(entry, "geyser") && HasAllProperties(entry, "geyser", "liquidType"),
		SaveWorldTransientRow.RadiationLineKind => HasAllProperties(entry, "radiationLine", "active", "timeGone"),
		_ => false,
	};

	/// <summary>
	/// Same rule for the two <c>run.json</c> rows. The native row's two required fields are
	/// both checked: an omitted <c>recipes</c> would deserialize to an empty list, which the
	/// applier would write absolutely — re-locking every recipe the player had unlocked —
	/// and an omitted <c>savedRunTime</c> would restart the run clock at zero. Neither
	/// default is distinguishable from a recorded value after deserialization, so the names
	/// are read off the raw JSON. <c>layerTimeSpent</c> is OPTIONAL by design: a layer-end
	/// cut and an archive written before the value existed both carry none, and the reader
	/// tells that apart from a recorded value by the property's presence (see
	/// <see cref="DecodeRun"/>), not by a default.
	/// </summary>
	private static bool CarriesItsOwnPayload(JsonElement entry, SaveRunRow row) => row.Kind switch
	{
		SaveRunRow.RunKind => HasAllProperties(entry, "run", "randomState"),
		SaveRunRow.NativeRunFieldsKind => HasAllProperties(entry, "nativeRunFields", "savedRunTime", "recipes"),
		_ => false,
	};

	/// <summary>
	/// A transient fact is keyed by its entity's WORLD POSITION, so the position must
	/// carry both coordinates: `"position": {}` or a half-written `{"y":9}` would
	/// deserialize to a real-looking `(0,0)` / `(0,9)` and be handed to the applier
	/// as if the fact belonged to that cell.
	/// </summary>
	private static bool HasPosition(JsonElement entry, string payloadName) =>
		entry.TryGetProperty(payloadName, out var payload)
		&& payload.ValueKind == JsonValueKind.Object
		&& payload.TryGetProperty("position", out var position)
		&& position.ValueKind == JsonValueKind.Object
		&& position.TryGetProperty("x", out var x)
		&& position.TryGetProperty("y", out var y)
		&& x.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
		&& y.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);

	private static bool HasAllProperties(JsonElement entry, string payloadName, params string[] propertyNames)
	{
		if (!entry.TryGetProperty(payloadName, out var payload) || payload.ValueKind != JsonValueKind.Object)
		{
			return false;
		}

		foreach (var name in propertyNames)
		{
			// Only the NAMES are checked, and a nested group counts as one property
			// (`keypad.position` is present or the row is malformed). That is enough
			// to reject the dangerous row — a payload missing its cell or its
			// position would otherwise deserialize to a real-looking (0,0) — without
			// inventing a field-by-field schema the format doc does not define.
			if (!payload.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
			{
				return false;
			}
		}

		return true;
	}

	private bool TryReadEpoch(out ulong epoch) =>
		ulong.TryParse(_manifest.RunEpoch, NumberStyles.None, CultureInfo.InvariantCulture, out epoch) && epoch != 0;

	private static void Add<T>(List<T> target, T? value)
	{
		if (value is not null)
		{
			target.Add(value);
		}
	}

	/// <summary>
	/// One entry, wire shape → kernel fact, inside this class's per-entry catch, and
	/// added to the domain ONLY when it materialized. The conversion is where an
	/// entry can actually be rejected (a wire form the kernel cannot represent, an
	/// unknown enum value), so it runs HERE — never in <see cref="Finish"/>, where
	/// one bad row would take its whole domain down (§6). The out-parameter shape
	/// matters: the kernel facts include structs, whose <c>default</c> is a
	/// perfectly valid-looking value — a rejected row must not become item 0.
	/// </summary>
	private void AddMapped<TWire, TKernel>(
		List<TKernel> target,
		JsonElement entry,
		SalvageSession session,
		string id,
		Func<TWire, TKernel> toKernel)
		where TWire : class
	{
		var wire = Decode<TWire>(entry, session, id);
		if (wire is not null && TryMaterialize(session, id, () => toKernel(wire), out var kernel))
		{
			target.Add(kernel);
		}
	}

	/// <summary>The same for a typed row whose payload has already been read (the enemy table's two shapes).</summary>
	private static void AddMapped<TWire, TKernel>(
		List<TKernel> target,
		JsonElement entry,
		SalvageSession session,
		string id,
		Func<TWire, TKernel> toKernel,
		SaveEnemyRow row,
		TWire wire)
		where TWire : class
	{
		if (TryMaterialize(session, row.Describe(), () => toKernel(wire), out var kernel))
		{
			target.Add(kernel);
		}
	}

	private static bool TryMaterialize<TKernel>(SalvageSession session, string id, Func<TKernel> materialize, out TKernel kernel)
	{
		try
		{
			kernel = materialize();
			return true;
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
		{
			session.Skip(id, "the entry could not be materialized into kernel state", ex.Message);
			kernel = default!;
			return false;
		}
	}

	/// <summary>Reads one entry; a failure is recorded as a per-entry skip and reported as null — never thrown.</summary>
	private T? Decode<T>(JsonElement entry, SalvageSession session, string id) where T : class
	{
		if (entry.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
		{
			// A JSON null is not a row: it deserializes to null without throwing, so
			// it would otherwise vanish from the account entirely (§6: every skipped
			// entry is surfaced).
			session.Skip(id, "the entry is JSON null, not a row of this domain", _currentPath);
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<T>(entry.GetRawText(), SaveArchiveJson.Options);
		}
		catch (JsonException ex)
		{
			session.Skip(id, $"the entry is not a readable {typeof(T).Name}", ex.Message);
			return null;
		}
	}

	/// <summary>A name for the damage report: the entry's own content id first, its position in the file otherwise.</summary>
	private static string IdOf(JsonElement entry, int index)
	{
		if (entry.ValueKind == JsonValueKind.Object)
		{
			if (entry.TryGetProperty("identity", out var identity)
				&& identity.ValueKind == JsonValueKind.Object
				&& identity.TryGetProperty("definitionId", out var definition)
				&& definition.ValueKind == JsonValueKind.String
				&& definition.GetString() is { Length: > 0 } definitionId)
			{
				return definitionId;
			}

			foreach (var name in new[] { "prefabId", "kind" })
			{
				if (entry.TryGetProperty(name, out var text) && text.ValueKind == JsonValueKind.String && text.GetString() is { Length: > 0 } value)
				{
					return value;
				}
			}

			if (entry.TryGetProperty("steamId", out var steamId) && steamId.ValueKind == JsonValueKind.Number)
			{
				return $"player {steamId}";
			}
		}

		return SalvageSession.IdOf(entry, index);
	}
}
