using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The game's own answer behind the console <c>/summon</c> verb — the counterpart of the game's own console
/// <c>spawn</c> command (<c>ConsoleScript.RegisterAllCommands</c>): the position words it accepts (<c>cursor</c> =
/// the pointer's world point, <c>player</c> = the local body, <c>random</c> = a point in the layer rectangle) and
/// ONE object summoned at a point, with the operator's type word.
///
/// <para>
/// The TYPE WORD is enforced here, against the game's own tables, because only this layer knows them.
/// <see cref="WorldSummonKind.Item"/> names a loose item entity: the id must be a definition in the game's item
/// table, its fact is captured from the prefab, the operator's state is bound against that capture and its
/// instance id comes from the one allocator — then the SAME <see cref="WorldItemCreation"/> the give ladder's drop
/// rung uses materializes and reports it, so a summoned item entity and a dropped one are the same fact and the
/// same world object. <see cref="WorldSummonKind.Entity"/> names a world-entity prefab (an animal included),
/// created through <c>Utils.Create</c> — the native call, extended to mod templates by
/// <c>UtilsCreateCustomPrefabPatch</c>, never a <c>Resources.Load</c> pre-check — and it must carry a
/// <c>BuildingEntity</c> with a prefab id a peer could recreate.
/// </para>
///
/// <para>
/// Neither family is re-notified here: the created object reports itself through its own landed path
/// (<c>BuildingEntity.Start</c> / <c>Item.Start</c> — the item path pre-stamps the id and reports explicitly, as
/// the drop rung does). This seam only creates and answers identity.
/// </para>
/// </summary>
internal sealed class WorldSummoner(
	IItemGrantSource grant,
	IItemControl items,
	ILogger<WorldSummoner> log) : IWorldSummon
{
	private readonly IItemGrantSource _grant = grant;
	private readonly ILogger _log = log;
	private readonly WorldItemCreation _worldItems = new(items, log);

	/// <summary>The native command's position words, read from the same three places the game's own <c>ParsePosition</c> reads.</summary>
	public bool TryResolveAnchor(WorldSummonAnchor anchor, out float x, out float y, out GrantRefusal? refusal)
	{
		x = 0f;
		y = 0f;
		refusal = null;

		if (!IsInWorld(out var sceneRefusal))
		{
			refusal = new GrantRefusal(
				sceneRefusal ?? "this client is not in a world, so there is no layer to create anything in.",
				"summon.answer.refused_not_in_world");
			return false;
		}

		switch (anchor)
		{
			case WorldSummonAnchor.Cursor:
				if (Camera.main == null) // Unity object — ==
				{
					refusal = new GrantRefusal(
						"this client has no camera, so the pointer cannot be read as a world point.",
						"summon.answer.refused_no_camera");
					return false;
				}

				var pointer = Camera.main.ScreenToWorldPoint(Input.mousePosition);
				x = pointer.x;
				y = pointer.y;
				return true;

			case WorldSummonAnchor.Player:
				var body = PlayerCamera.main != null ? PlayerCamera.main.body : null; // Unity object — ==
				if (body == null) // Unity object — ==
				{
					refusal = new GrantRefusal(
						"this client's body cannot be read right now, so 'player' has no position.",
						"summon.answer.refused_no_body");
					return false;
				}

				x = body.transform.position.x;
				y = body.transform.position.y;
				return true;

			case WorldSummonAnchor.Random:
				// The native formula (ConsoleScript.ParsePosition): a uniform point in the layer rectangle.
				x = Random.Range(-1f, 1f) * WorldGeneration.world.halfWidth;
				y = Random.Range(-1f, 1f) * WorldGeneration.world.halfHeight;
				return true;

			default:
				refusal = new GrantRefusal(
					$"'{anchor}' is not a position this build knows.",
					"summon.answer.refused_unknown_anchor",
					anchor.ToString());
				return false;
		}
	}

	/// <inheritdoc />
	public WorldSummonOutcome TrySummon(string prefabId, WorldSummonKind kind, SnbtValue? state, float x, float y)
	{
		if (string.IsNullOrWhiteSpace(prefabId))
		{
			return Refused(new GrantRefusal(
				"no prefab id was given.",
				"summon.answer.refused_no_prefab_id"));
		}

		if (!IsInWorld(out var worldRefusal))
		{
			return Refused(new GrantRefusal(
				worldRefusal!,
				"summon.answer.refused_position_unavailable",
				worldRefusal ?? string.Empty));
		}

		var isItemDefinition = Item.GlobalItems?.ContainsKey(prefabId) == true;
		if (kind == WorldSummonKind.Item)
		{
			if (!isItemDefinition)
			{
				return Refused(new GrantRefusal(
					$"'/summon item' names an item definition from the game's own item table, and '{prefabId}' is not one. An entity prefab is summoned without the word: /summon {prefabId}. Nothing was created.",
					"summon.answer.refused_unknown_item_prefab",
					prefabId));
			}

			return SummonItem(prefabId, state, x, y);
		}

		if (isItemDefinition)
		{
			// The bare form names a world entity; an item definition belongs to the other type word. Refused
			// BEFORE anything is created, so no orphan object can outlive the answer.
			return Refused(new GrantRefusal(
				$"'{prefabId}' is an item definition, so it is summoned as a loose item entity: /summon item {prefabId}{StateHint(state)}. Nothing was created.",
				"summon.answer.refused_item_needs_the_word",
				prefabId,
				StateHint(state)));
		}

		if (state is not null)
		{
			// An entity's creation-time values (a geyser's liquid type, a keypad's code, a crystal's tint) are the
			// game's own, and this tree has no entity state document.
			return Refused(new GrantRefusal(
				$"'{prefabId}' is not an item definition, so it takes no state argument — an entity's creation-time values are the game's own. Nothing was created.",
				"summon.answer.refused_entity_takes_no_state",
				prefabId));
		}

		return SummonEntity(prefabId, x, y);
	}

	/// <summary>One loose item entity: the fact (id + bound state) from the creation port, then the world object and its report.</summary>
	private WorldSummonOutcome SummonItem(string prefabId, SnbtValue? state, float x, float y)
	{
		if (!_grant.TryCreate(prefabId, state, out var item, out var refusal) || item is null)
		{
			return refusal is not null
				? Refused(refusal)
				: Refused(new GrantRefusal(
					$"the item definition '{prefabId}' could not be created by this build.",
					"item.answer.refused_unknown_definition",
					prefabId));
		}

		if (!_worldItems.TryCreateAt(new Vector3(x, y, 0f), item, "Summon", out var createRefusal))
		{
			return Refused(new GrantRefusal(
				createRefusal ?? $"the item '{prefabId}' could not be materialized at the position.",
				"summon.answer.refused_materialize",
				prefabId));
		}

		// The answer names the state the item REALLY carries: the bind may derive or drop a value (a container's
		// condition is its fill ratio, a zero stack is dropped), so the operator's text is not the truth here.
		var stateText = state is null ? "" : SnbtWriter.Write(ItemStateDocument.Describe(item));
		return new WorldSummonOutcome(true, item.InstanceId, stateText, null);
	}

	/// <summary>One world entity (an animal included — the created BuildingEntity's own flags decide how it reports).</summary>
	private WorldSummonOutcome SummonEntity(string prefabId, float x, float y)
	{
		// The attempt IS the check: a mod-registered template is not in Resources, and Utils.Create throws on a
		// missing prefab (UtilsCreateCustomPrefabPatch serves the custom ones). The factory contains that throw,
		// refuses an object without a BuildingEntity (destroying the orphan) and logs the concrete ground.
		var created = RuntimeEntityFactory.TryCreate(prefabId, new Vector2(x, y), _log, "Summon");
		if (created == null) // Unity object — ==
		{
			return Refused(new GrantRefusal(
				$"no prefab '{prefabId}' could be materialized: it is not an item definition this build knows, and no world-entity prefab by that id exists.",
				"summon.answer.refused_unknown_prefab",
				prefabId));
		}

		if (string.IsNullOrEmpty(created.id))
		{
			// A prefab with no entity id cannot be reported — the creation message carries the id a peer
			// recreates from — so it would exist on this side only. Destroy it instead of leaving the divergence.
			Object.Destroy(created.gameObject);
			return Refused(new GrantRefusal(
				$"the prefab '{prefabId}' carries no entity id, so no peer could recreate it. Nothing was created.",
				"summon.answer.refused_no_entity_id",
				prefabId));
		}

		_log.LogInformation("[Summon] created {Prefab} at ({X:F1},{Y:F1}); its own Start report replicates it.", prefabId, x, y);
		return new WorldSummonOutcome(true, 0, "", null);
	}

	/// <summary>The state hint a refusal echoes back when the operator attached one, so the corrected spelling keeps their edit.</summary>
	private static string StateHint(SnbtValue? state) => state is null ? "" : " {<state>}";

	/// <summary>
	/// Whether this client can create a world object at all: the game's own world must exist (the native
	/// command's <c>CheckForWorld</c>). A summon in a menu or during generation has no layer to place anything in,
	/// and reporting a position from it would name a world that does not exist yet.
	/// </summary>
	private static bool IsInWorld(out string? refusal)
	{
		if (WorldGeneration.world == null || !WorldGeneration.world.worldExists) // Unity object — ==
		{
			refusal = "this client is not in a world, so there is no layer to create anything in.";
			return false;
		}

		refusal = null;
		return true;
	}

	/// <summary>A refusal whose KEY is chosen at the call site (where the reason is known), so the reader's console renders it in the reader's language.</summary>
	private static WorldSummonOutcome Refused(GrantRefusal refusal) => new(false, 0, "", refusal);
}
