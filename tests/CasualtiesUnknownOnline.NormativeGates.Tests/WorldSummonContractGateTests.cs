using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The console <c>/summon</c> verb's ADAPTER half is code no behavioural suite can compile: the test project
/// references GameAdapter with <c>ExcludeAssets="compile"</c>, and every summon suite replaces the seam with
/// <c>WorldSummoner</c>'s hand-written stand-in. The production type-word classification, the BRANCH CONDITIONS
/// that choose a family, every refusal sentence as a full literal (tails included), the state hint's returned
/// value, the three position words with their own refusals, and the two creation calls are therefore pinned here
/// by source shape — the same evidence shape the give ladder's drop chain uses.
///
/// <para>
/// This is a TEXT pin, not an execution: it proves the production source still carries the sentence it answers
/// with AND the condition that chooses it. That is what makes the behavioural suites' fake sentences traceable to
/// one production text. Its own matcher is pinned by a complete sample and one missing-marker case per check, so a
/// rule that quietly stopped seeing a marker fails instead of passing by checking nothing.
/// </para>
/// </summary>
public class WorldSummonContractGateTests
{
	private const string SummonerFile = "src/CasualtiesUnknownOnline.GameAdapter/World/WorldSummoner.cs";

	[Fact]
	public void WorldSummoner_EnforcesTheTypeWordAndCreatesThroughTheSharedPorts()
	{
		var path = RepositoryPaths.File(SummonerFile);
		Assert.True(File.Exists(path), $"the summoner moved: {SummonerFile}");

		var failures = SummonerContractFailures(File.ReadAllText(path));
		Assert.True(failures.Count == 0, SummonerFile + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void TheSummonerContractMatcher_PassesItsOwnCompleteSample() =>
		Assert.Empty(SummonerContractFailures(CompleteSample));

	/// <summary>Each case removes ONE marker the gate claims to read; exactly that check must fail, so a matcher that lost a marker cannot pass silently.</summary>
	public static TheoryData<string, string> OneMarkerRemoved => new()
	{
		{ "var isItemDefinition = Item.GlobalItems?.ContainsKey(prefabId) == true;", "var isItemDefinition = false;" },
		{ "if (kind == WorldSummonKind.Item)", "if (kind == WorldSummonKind.Entity)" },
		{ "if (!isItemDefinition)", "if (notAnItem)" },
		{ "'/summon item' names an item definition from the game's own item table, and '{prefabId}' is not one. An entity prefab is summoned without the word: /summon {prefabId}. Nothing was created.", "lost item-word sentence" },
		{ "if (isItemDefinition)", "if (anItem)" },
		{ "'{prefabId}' is an item definition, so it is summoned as a loose item entity: /summon item {prefabId}{StateHint(state)}. Nothing was created.", "lost bare-item sentence" },
		{ "if (state is not null)", "if (state is null)" },
		{ "'{prefabId}' is not an item definition, so it takes no state argument — an entity's creation-time values are the game's own. Nothing was created.", "lost entity-state sentence" },
		{ "return SummonItem(prefabId, state, x, y);", "return Refused(\"\");" },
		{ "return SummonEntity(prefabId, x, y);", "return Refused(\"\");" },
		{ "no prefab id was given.", "no id" },
		{ "if (!IsInWorld(out var worldRefusal))", "if (false)" },
		{ "if (!_grant.TryCreate(prefabId, state, out var item, out var refusal) || item is null)", "if (!_grant.TryCreate(prefabId))" },
		{ "the item definition '{prefabId}' could not be created by this build.", "lost fact-refusal sentence" },
		{ "if (!_worldItems.TryCreateAt(new Vector3(x, y, 0f), item, \"Summon\", out var createRefusal))", "if (!_worldItems.TryCreateAt(item))" },
		{ "the item '{prefabId}' could not be materialized at the position.", "lost materialization-refusal sentence" },
		{ "var stateText = state is null ? \"\" : SnbtWriter.Write(ItemStateDocument.Describe(item));", "var stateText = \"\";" },
		{ "var created = RuntimeEntityFactory.TryCreate(prefabId, new Vector2(x, y), _log, \"Summon\");", "var created = RuntimeEntityFactory.TryCreate(prefabId);" },
		{ "if (created == null)", "if (created != null)" },
		{ "no prefab '{prefabId}' could be materialized: it is not an item definition this build knows, and no world-entity prefab by that id exists.", "lost unknown-prefab sentence" },
		{ "if (string.IsNullOrEmpty(created.id))", "if (created.id is null)" },
		{ "Object.Destroy(created.gameObject)", "Object.Keep(created.gameObject)" },
		{ "the prefab '{prefabId}' carries no entity id, so no peer could recreate it. Nothing was created.", "lost id-recreatable sentence" },
		{ "switch (anchor)", "switch (kind)" },
		{ "case WorldSummonAnchor.Cursor:", "case WorldSummonAnchor.Player:" },
		{ "var pointer = Camera.main.ScreenToWorldPoint(Input.mousePosition);", "var pointer = Vector2.zero;" },
		{ "this client has no camera, so the pointer cannot be read as a world point.", "lost camera sentence" },
		{ "case WorldSummonAnchor.Player:", "case WorldSummonAnchor.Random:" },
		{ "var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;", "var body = null;" },
		{ "this client's body cannot be read right now, so 'player' has no position.", "lost body sentence" },
		{ "case WorldSummonAnchor.Random:", "case WorldSummonAnchor.Cursor:" },
		{ "x = Random.Range(-1f, 1f) * WorldGeneration.world.halfWidth;", "x = 0f;" },
		{ "y = Random.Range(-1f, 1f) * WorldGeneration.world.halfHeight;", "y = 0f;" },
		{ "'{anchor}' is not a position this build knows.", "lost position-word sentence" },
		{ "if (!IsInWorld(out var sceneRefusal))", "if (false)" },
		{ "if (WorldGeneration.world == null || !WorldGeneration.world.worldExists)", "if (false)" },
		{ "this client is not in a world, so there is no layer to create anything in.", "lost not-in-world sentence" },
		{ "=> state is null ? \"\" : \" {<state>}\"", "=> \"\"" },
		{ "private readonly WorldItemCreation _worldItems = new(items, log);", "private readonly WorldItemCreation _worldItems = null!;" },
	};

	[Theory]
	[MemberData(nameof(OneMarkerRemoved))]
	public void TheSummonerContractMatcher_FailsWhenOneMarkerIsGone(string marker, string replacement)
	{
		Assert.Contains(marker, CompleteSample, StringComparison.Ordinal);
		var failures = SummonerContractFailures(CompleteSample.Replace(marker, replacement, StringComparison.Ordinal));
		Assert.Single(failures);
	}

	[Fact]
	public void TheSummonerContractMatcher_FailsWhenAMethodIsMissing() =>
		Assert.Equal(7, SummonerContractFailures("internal sealed class WorldSummoner { }").Count);

	private static List<string> SummonerContractFailures(string text)
	{
		var failures = new List<string>();
		var trySummon = SourceScan.MethodBody(text, "public WorldSummonOutcome TrySummon(string prefabId, WorldSummonKind kind, SnbtValue? state, float x, float y)");
		if (trySummon.Length == 0)
		{
			failures.Add("TrySummon() not found — the type-word classification would pass by checking nothing");
		}
		else
		{
			Require(trySummon, "var isItemDefinition = Item.GlobalItems?.ContainsKey(prefabId) == true;",
				"TrySummon() no longer asks the game's own item table which family the id belongs to", failures);
			Require(trySummon, "if (kind == WorldSummonKind.Item)",
				"the type word no longer chooses the item branch", failures);
			Require(trySummon, "if (!isItemDefinition)",
				"the item word no longer refuses an id that is not in the game's item table", failures);
			Require(trySummon, "'/summon item' names an item definition from the game's own item table, and '{prefabId}' is not one. An entity prefab is summoned without the word: /summon {prefabId}. Nothing was created.",
				"the refusal for the item word with a non-item prefab lost its sentence", failures);
			Require(trySummon, "if (isItemDefinition)",
				"the bare form no longer distinguishes an item definition from an entity prefab", failures);
			Require(trySummon, "'{prefabId}' is an item definition, so it is summoned as a loose item entity: /summon item {prefabId}{StateHint(state)}. Nothing was created.",
				"the bare item definition's refusal lost its sentence or the operator's state hint", failures);
			Require(trySummon, "if (state is not null)",
				"an entity prefab's state argument is no longer detected before the creation", failures);
			Require(trySummon, "'{prefabId}' is not an item definition, so it takes no state argument — an entity's creation-time values are the game's own. Nothing was created.",
				"the refusal for an entity prefab carrying a state lost its sentence", failures);
			Require(trySummon, "no prefab id was given.",
				"an empty prefab id no longer has its own refusal", failures);
			Require(trySummon, "if (!IsInWorld(out var worldRefusal))",
				"TrySummon() no longer checks that this client is in a world", failures);
			Require(trySummon, "return SummonItem(prefabId, state, x, y);",
				"the item branch no longer dispatches to SummonItem", failures);
			Require(trySummon, "return SummonEntity(prefabId, x, y);",
				"the entity branch no longer dispatches to SummonEntity", failures);
		}

		var summonItem = SourceScan.MethodBody(text, "private WorldSummonOutcome SummonItem(string prefabId, SnbtValue? state, float x, float y)");
		if (summonItem.Length == 0)
		{
			failures.Add("SummonItem() not found — the item entity path would pass by checking nothing");
		}
		else
		{
			Require(summonItem, "if (!_grant.TryCreate(prefabId, state, out var item, out var refusal) || item is null)",
				"SummonItem() no longer creates the fact through the give verb's creation port", failures);
			Require(summonItem, "the item definition '{prefabId}' could not be created by this build.",
				"the refusal a creation port answers without its own reason lost its sentence", failures);
			Require(summonItem, "if (!_worldItems.TryCreateAt(new Vector3(x, y, 0f), item, \"Summon\", out var createRefusal))",
				"SummonItem() no longer materializes through the give drop's shared WorldItemCreation", failures);
			Require(summonItem, "the item '{prefabId}' could not be materialized at the position.",
				"the refusal for an unmaterializable item lost its sentence", failures);
			Require(summonItem, "var stateText = state is null ? \"\" : SnbtWriter.Write(ItemStateDocument.Describe(item));",
				"SummonItem()'s answer no longer names the state the created item really carries", failures);
		}

		var summonEntity = SourceScan.MethodBody(text, "private WorldSummonOutcome SummonEntity(string prefabId, float x, float y)");
		if (summonEntity.Length == 0)
		{
			failures.Add("SummonEntity() not found — the entity path would pass by checking nothing");
		}
		else
		{
			Require(summonEntity, "var created = RuntimeEntityFactory.TryCreate(prefabId, new Vector2(x, y), _log, \"Summon\");",
				"SummonEntity() no longer creates through the game's own Utils.Create factory", failures);
			Require(summonEntity, "if (created == null)",
				"a failed creation is no longer detected before the id check", failures);
			Require(summonEntity, "no prefab '{prefabId}' could be materialized: it is not an item definition this build knows, and no world-entity prefab by that id exists.",
				"the refusal for a prefab neither table knows lost its sentence", failures);
			Require(summonEntity, "if (string.IsNullOrEmpty(created.id))",
				"a created prefab with no entity id is no longer detected", failures);
			Require(summonEntity, "Object.Destroy(created.gameObject)",
				"an entity prefab without an id is no longer destroyed, so it would exist on the host only", failures);
			Require(summonEntity, "the prefab '{prefabId}' carries no entity id, so no peer could recreate it. Nothing was created.",
				"the refusal for an entity prefab with no recreatable id lost its sentence", failures);
		}

		var resolveAnchor = SourceScan.MethodBody(text, "public bool TryResolveAnchor(WorldSummonAnchor anchor, out float x, out float y, out GrantRefusal? refusal)");
		if (resolveAnchor.Length == 0)
		{
			failures.Add("TryResolveAnchor() not found — the position words would pass by checking nothing");
		}
		else
		{
			Require(resolveAnchor, "if (!IsInWorld(out var sceneRefusal))",
				"TryResolveAnchor() no longer checks that this client is in a world", failures);
			Require(resolveAnchor, "switch (anchor)",
				"the position word is no longer the switch", failures);
			Require(resolveAnchor, "case WorldSummonAnchor.Cursor:",
				"the pointer position word is gone", failures);
			Require(resolveAnchor, "var pointer = Camera.main.ScreenToWorldPoint(Input.mousePosition);",
				"'cursor' no longer reads the game's own pointer world point", failures);
			Require(resolveAnchor, "this client has no camera, so the pointer cannot be read as a world point.",
				"the pointer's camera refusal lost its sentence", failures);
			Require(resolveAnchor, "case WorldSummonAnchor.Player:",
				"the body position word is gone", failures);
			Require(resolveAnchor, "var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;",
				"'player' no longer reads the local body", failures);
			Require(resolveAnchor, "this client's body cannot be read right now, so 'player' has no position.",
				"the body's refusal lost its sentence", failures);
			Require(resolveAnchor, "case WorldSummonAnchor.Random:",
				"the random position word is gone", failures);
			Require(resolveAnchor, "x = Random.Range(-1f, 1f) * WorldGeneration.world.halfWidth;",
				"'random' no longer takes its x from the layer rectangle", failures);
			Require(resolveAnchor, "y = Random.Range(-1f, 1f) * WorldGeneration.world.halfHeight;",
				"'random' no longer takes its y from the layer rectangle", failures);
			Require(resolveAnchor, "'{anchor}' is not a position this build knows.",
				"an unknown position word lost its refusal", failures);
		}

		var inWorld = SourceScan.MethodBody(text, "private static bool IsInWorld(out string? refusal)");
		if (inWorld.Length == 0)
		{
			failures.Add("IsInWorld() not found — the world check would pass by checking nothing");
		}
		else
		{
			Require(inWorld, "if (WorldGeneration.world == null || !WorldGeneration.world.worldExists)",
				"the world check no longer reads the game's own world existence", failures);
			Require(inWorld, "this client is not in a world, so there is no layer to create anything in.",
				"the not-in-world refusal lost its sentence", failures);
		}

		Require(text, "=> state is null ? \"\" : \" {<state>}\"",
			"the state hint no longer echoes the operator's attached state", failures);
		Require(text, "private readonly WorldItemCreation _worldItems = new(items, log);",
			"the summoner stopped owning the same WorldItemCreation the give ladder's drop uses", failures);

		return failures;
	}

	private static void Require(string body, string marker, string failure, List<string> failures)
	{
		if (!body.Contains(marker, StringComparison.Ordinal))
		{
			failures.Add(failure);
		}
	}

	/// <summary>The complete sample: every method and field the gate reads, carrying every marker in the production text.</summary>
	private const string CompleteSample = """
		internal sealed class WorldSummoner(IItemGrantSource grant, IItemControl items, ILogger<WorldSummoner> log) : IWorldSummon
		{
			private readonly WorldItemCreation _worldItems = new(items, log);

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
						if (Camera.main == null)
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
						var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
						if (body == null)
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

			public WorldSummonOutcome TrySummon(string prefabId, WorldSummonKind kind, SnbtValue? state, float x, float y)
			{
				if (string.IsNullOrWhiteSpace(prefabId))
				{
					return Refused("no prefab id was given.");
				}

				if (!IsInWorld(out var worldRefusal))
				{
					return Refused(worldRefusal!);
				}

				var isItemDefinition = Item.GlobalItems?.ContainsKey(prefabId) == true;
				if (kind == WorldSummonKind.Item)
				{
					if (!isItemDefinition)
					{
						return Refused($"'/summon item' names an item definition from the game's own item table, and '{prefabId}' is not one. An entity prefab is summoned without the word: /summon {prefabId}. Nothing was created.");
					}

					return SummonItem(prefabId, state, x, y);
				}

				if (isItemDefinition)
				{
					return Refused($"'{prefabId}' is an item definition, so it is summoned as a loose item entity: /summon item {prefabId}{StateHint(state)}. Nothing was created.");
				}

				if (state is not null)
				{
					return Refused($"'{prefabId}' is not an item definition, so it takes no state argument — an entity's creation-time values are the game's own. Nothing was created.");
				}

				return SummonEntity(prefabId, x, y);
			}

			private WorldSummonOutcome SummonItem(string prefabId, SnbtValue? state, float x, float y)
			{
				if (!_grant.TryCreate(prefabId, state, out var item, out var refusal) || item is null)
				{
					return Refused(refusal ?? $"the item definition '{prefabId}' could not be created by this build.");
				}

				if (!_worldItems.TryCreateAt(new Vector3(x, y, 0f), item, "Summon", out var createRefusal))
				{
					return Refused(createRefusal ?? $"the item '{prefabId}' could not be materialized at the position.");
				}

				var stateText = state is null ? "" : SnbtWriter.Write(ItemStateDocument.Describe(item));
				return new WorldSummonOutcome(true, item.InstanceId, stateText, null);
			}

			private WorldSummonOutcome SummonEntity(string prefabId, float x, float y)
			{
				var created = RuntimeEntityFactory.TryCreate(prefabId, new Vector2(x, y), _log, "Summon");
				if (created == null)
				{
					return Refused($"no prefab '{prefabId}' could be materialized: it is not an item definition this build knows, and no world-entity prefab by that id exists.");
				}

				if (string.IsNullOrEmpty(created.id))
				{
					Object.Destroy(created.gameObject);
					return Refused($"the prefab '{prefabId}' carries no entity id, so no peer could recreate it. Nothing was created.");
				}

				return new WorldSummonOutcome(true, 0, "", null);
			}

			private static string StateHint(SnbtValue? state) => state is null ? "" : " {<state>}";

			private static bool IsInWorld(out string? refusal)
			{
				if (WorldGeneration.world == null || !WorldGeneration.world.worldExists)
				{
					refusal = "this client is not in a world, so there is no layer to create anything in.";
					return false;
				}

				refusal = null;
				return true;
			}

			private static WorldSummonOutcome Refused(string reason) => new(false, 0, "", reason);
		}
		""";
}
