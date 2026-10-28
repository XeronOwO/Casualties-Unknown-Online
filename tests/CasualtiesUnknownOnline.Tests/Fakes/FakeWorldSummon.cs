using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The Game Adapter's scene behind the console <c>/summon</c> verb, as a test's own fixture: the native position
/// words answered from configured points, and one object "summoned" and REPORTED through the real landed path for
/// its family — <see cref="IWorldControl.SendEntitySpawned"/> for an entity or an animal (the report the adapter's
/// <c>BuildingEntity.Start</c> hook makes) and <see cref="IItemControl.SendItemSpawned"/> for a loose item entity
/// (the report <c>WorldItemCreation</c> makes, with the fixture's own id and the shared state bind — the same fact
/// the give ladder's drop produces).
///
/// <para>
/// That report is the whole point of the fixture: the suites that use it prove the claim this stage had to answer
/// — a later joiner receives the creation through its family's absolute table — against the REAL channel,
/// registry and world-entry fan-out, while only the game-assembly half (instantiation and the tables) is faked.
/// The TYPE WORD is enforced here the way the real seam enforces it: an item id without <c>item</c> is refused with the
/// spelling that works, <c>item</c> with a non-item prefab is refused by name, an entity prefab given a state is
/// refused before anything is "created", and the anchor map can be made to refuse so the no-world path is
/// reachable. The sentences mirror <c>WorldSummoner</c>'s, whose production text is pinned by
/// <c>WorldSummonContractGateTests</c>: this fixture stands in for the seam's scene half, not for its wording.
/// </para>
/// </summary>
internal sealed class FakeWorldSummon : IWorldSummon
{
	private readonly IItemControl _items;
	private readonly IWorldControl _world;
	private readonly ISessionControl _session;
	private ulong _nextItemId = 700000;
	private uint _creationSequence;

	internal FakeWorldSummon(IItemControl items, IWorldControl world, ISessionControl session)
	{
		_items = items;
		_world = world;
		_session = session;
	}

	/// <summary>The points the position words answer; a suite overrides the ones it stages.</summary>
	internal Dictionary<WorldSummonAnchor, (float X, float Y)> Anchors { get; } = new()
	{
		[WorldSummonAnchor.Cursor] = (10f, 20f),
		[WorldSummonAnchor.Player] = (1f, 2f),
		[WorldSummonAnchor.Random] = (5f, 6f),
	};

	/// <summary>When set, every position word is refused with this sentence (the "no world / no body" path).</summary>
	internal string? AnchorRefusal { get; set; }

	/// <summary>The entity prefabs this fixture can materialize.</summary>
	internal HashSet<string> EntityPrefabs { get; } = ["beartrap", "medcrate"];

	/// <summary>Which of them are animals (<c>BuildingEntity.animal</c>) — reported on the creation channel's animal branch.</summary>
	internal HashSet<string> Animals { get; } = ["spider"];

	/// <summary>Every prefab the verb asked for, in order, with its type word and the state text it carried (empty = none).</summary>
	internal List<(string PrefabId, WorldSummonKind Kind, string State)> Attempts { get; } = [];

	/// <summary>Every item entity this fixture minted, in order.</summary>
	internal List<CharacterItemMsg> CreatedItems { get; } = [];

	/// <summary>The last position word resolved, or null when the invocation named a literal point.</summary>
	internal WorldSummonAnchor? LastAnchor { get; private set; }

	public bool TryResolveAnchor(WorldSummonAnchor anchor, out float x, out float y, out string? refusal)
	{
		LastAnchor = anchor;
		if (AnchorRefusal is not null)
		{
			x = 0f;
			y = 0f;
			refusal = AnchorRefusal;
			return false;
		}

		(x, y) = Anchors[anchor];
		refusal = null;
		return true;
	}

	public WorldSummonOutcome TrySummon(string prefabId, WorldSummonKind kind, SnbtValue? state, float x, float y)
	{
		Attempts.Add((prefabId, kind, state is null ? "" : SnbtWriter.Write(state)));
		var definition = FakeItemState.Definition(prefabId);

		if (kind == WorldSummonKind.Item)
		{
			if (definition is null)
			{
				return new WorldSummonOutcome(false, 0, "", new GrantRefusal(
					$"'/summon item' names an item definition from the game's own item table, and '{prefabId}' is not one. An entity prefab is summoned without the word: /summon {prefabId}. Nothing was created.",
					"summon.answer.refused_unknown_item_prefab",
					prefabId));
			}

			return SummonItem(prefabId, definition, state, x, y);
		}

		if (definition is not null)
		{
			var hint = state is null ? "" : " {<state>}";
			return new WorldSummonOutcome(false, 0, "", new GrantRefusal(
				$"'{prefabId}' is an item definition, so it is summoned as a loose item entity: /summon item {prefabId}{hint}. Nothing was created.",
				"summon.answer.refused_item_needs_the_word",
				prefabId,
				hint));
		}

		if (state is not null)
		{
			return new WorldSummonOutcome(false, 0, "", new GrantRefusal(
				$"'{prefabId}' is not an item definition, so it takes no state argument — an entity's creation-time values are the game's own. Nothing was created.",
				"summon.answer.refused_entity_takes_no_state",
				prefabId));
		}

		if (!EntityPrefabs.Contains(prefabId) && !Animals.Contains(prefabId))
		{
			return new WorldSummonOutcome(false, 0, "", new GrantRefusal(
				$"no prefab '{prefabId}' could be materialized: it is not an item definition this build knows, and no world-entity prefab by that id exists.",
				"summon.answer.refused_unknown_prefab",
				prefabId));
		}

		// The report the adapter's BuildingEntity.Start hook makes for a creation outside generation.
		_world.SendEntitySpawned(new EntitySpawnedMsg
		{
			Id = prefabId,
			Position = new NetVector2Msg(x, y),
			Rotation = 0f,
			IsAnimal = Animals.Contains(prefabId),
			CreatorSteamId = _session.LocalSteamId,
			CreationSequence = ++_creationSequence,
		});
		return new WorldSummonOutcome(true, 0, "", null);
	}

	private WorldSummonOutcome SummonItem(string prefabId, CharacterItemMsg capture, SnbtValue? state, float x, float y)
	{
		var document = state is null
			? FakeItemState.Describe(capture)
			: SnbtTree.Overlay(FakeItemState.Describe(capture), state);
		if (!FakeItemState.TryBind(capture, document, out var bound, out var refusal))
		{
			return new WorldSummonOutcome(false, 0, "", new GrantRefusal(
				refusal ?? "the state text was not accepted.",
				"item.answer.refused_state_text",
				refusal ?? string.Empty));
		}

		bound.InstanceId = _nextItemId++;
		bound.SlotIndex = -1;
		CreatedItems.Add(bound);

		// The report WorldItemCreation makes: the row the host keeps and the peers materialize — the same row
		// the give ladder's drop produces.
		_items.SendItemSpawned(
			bound.InstanceId,
			bound,
			new NetVector2(x, y),
			new NetVector2(0f, 0f),
			0f,
			freshItemDrop: false,
			angularVelocity: 0f);
		return new WorldSummonOutcome(true, bound.InstanceId, state is null ? "" : SnbtWriter.Write(FakeItemState.Describe(bound)), null);
	}
}
