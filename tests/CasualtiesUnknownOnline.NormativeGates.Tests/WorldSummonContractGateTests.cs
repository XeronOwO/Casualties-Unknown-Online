using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The console <c>/summon</c> verb's ADAPTER half is code no behavioural suite can compile: the test project
/// references GameAdapter with <c>ExcludeAssets="compile"</c>, and every summon suite replaces the seam with
/// <c>WorldSummoner</c>'s hand-written stand-in. So the type-word classification, every refusal sentence
/// and the two creation calls live in a file whose only runtime proof is the acceptance batch — they are pinned
/// here by source shape, the same evidence shape the give ladder's drop chain uses.
///
/// <para>
/// The gate reads <c>WorldSummoner</c>'s two family methods and the class field that owns the shared creation:
/// a family that stops reaching its port, a refusal that loses its sentence, the state hint, the id-recreatable
/// guard, or the shared <c>WorldItemCreation</c> fails here. The behavioural suites still prove the console and
/// the service against the seam's CONTRACT (the fake answers the same sentences); this gate is what makes the
/// production sentences the ones that contract was written from. Its own matcher is pinned by a complete sample
/// and one missing-marker case per check, so a rule that quietly stopped seeing a marker fails instead of
/// passing by checking nothing.
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
	[Theory]
	[InlineData("Item.GlobalItems", "Item.OtherTable")]
	[InlineData("'/summon item' names an item definition", "'/summon item' names nothing")]
	[InlineData("StateHint(state)", "state")]
	[InlineData("takes no state argument", "takes anything")]
	[InlineData("return SummonItem(prefabId, state, x, y);", "return Refused(\"\");")]
	[InlineData("_grant.TryCreate(prefabId, state, out var item, out var refusal)", "_grant.Create(prefabId)")]
	[InlineData("_worldItems.TryCreateAt(new Vector3(x, y, 0f), item, \"Summon\", out var createRefusal)", "_worldItems.TryCreateAt(item)")]
	[InlineData("SnbtWriter.Write(ItemStateDocument.Describe(item))", "\"\"")]
	[InlineData("RuntimeEntityFactory.TryCreate(prefabId, new Vector2(x, y), _log, \"Summon\")", "RuntimeEntityFactory.TryCreate(prefabId)")]
	[InlineData("Object.Destroy(created.gameObject)", "Object.Keep(created.gameObject)")]
	[InlineData("carries no entity id", "carries an id")]
	[InlineData("return SummonEntity(prefabId, x, y);", "return Refused(\"\");")]
	[InlineData("private readonly WorldItemCreation _worldItems = new(items, log);", "private readonly WorldItemCreation _worldItems = null!;")]
	public void TheSummonerContractMatcher_FailsWhenOneMarkerIsGone(string marker, string replacement)
	{
		Assert.Contains(marker, CompleteSample);
		var failures = SummonerContractFailures(CompleteSample.Replace(marker, replacement, StringComparison.Ordinal));
		Assert.Single(failures);
	}

	[Fact]
	public void TheSummonerContractMatcher_FailsWhenAMethodIsMissing() =>
		Assert.Equal(4, SummonerContractFailures("internal sealed class WorldSummoner { }").Count);

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
			Require(trySummon, "Item.GlobalItems",
				"TrySummon() no longer asks the game's own item table which family the id belongs to", failures);
			Require(trySummon, "'/summon item' names an item definition",
				"the refusal for the item word with a non-item prefab lost its sentence", failures);
			Require(trySummon, "StateHint(state)",
				"the bare item definition's refusal no longer echoes the operator's attached state", failures);
			Require(trySummon, "takes no state argument",
				"the refusal for an entity prefab carrying a state lost its sentence", failures);
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
			Require(summonItem, "_grant.TryCreate(prefabId, state, out var item, out var refusal)",
				"SummonItem() no longer creates the fact through the give verb's creation port", failures);
			Require(summonItem, "_worldItems.TryCreateAt(new Vector3(x, y, 0f), item, \"Summon\", out var createRefusal)",
				"SummonItem() no longer materializes through the give drop's shared WorldItemCreation", failures);
			Require(summonItem, "SnbtWriter.Write(ItemStateDocument.Describe(item))",
				"SummonItem()'s answer no longer names the state the created item really carries", failures);
		}

		var summonEntity = SourceScan.MethodBody(text, "private WorldSummonOutcome SummonEntity(string prefabId, float x, float y)");
		if (summonEntity.Length == 0)
		{
			failures.Add("SummonEntity() not found — the entity path would pass by checking nothing");
		}
		else
		{
			Require(summonEntity, "RuntimeEntityFactory.TryCreate(prefabId, new Vector2(x, y), _log, \"Summon\")",
				"SummonEntity() no longer creates through the game's own Utils.Create factory", failures);
			Require(summonEntity, "Object.Destroy(created.gameObject)",
				"an entity prefab without an id is no longer destroyed, so it would exist on the host only", failures);
			Require(summonEntity, "carries no entity id",
				"the refusal for an entity prefab with no recreatable id lost its sentence", failures);
		}

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

	/// <summary>The complete sample: the two family methods and the field, carrying every marker the gate reads.</summary>
	private const string CompleteSample = """
		internal sealed class WorldSummoner(IItemGrantSource grant, IItemControl items, ILogger<WorldSummoner> log) : IWorldSummon
		{
			private readonly WorldItemCreation _worldItems = new(items, log);

			public WorldSummonOutcome TrySummon(string prefabId, WorldSummonKind kind, SnbtValue? state, float x, float y)
			{
				var isItemDefinition = Item.GlobalItems?.ContainsKey(prefabId) == true;
				if (kind == WorldSummonKind.Item)
				{
					if (!isItemDefinition)
					{
						return Refused($"'/summon item' names an item definition from the game's own item table, and '{prefabId}' is not one.");
					}

					return SummonItem(prefabId, state, x, y);
				}

				if (isItemDefinition)
				{
					return Refused($"'{prefabId}' is an item definition, so it is summoned as a loose item entity: /summon item {prefabId}{StateHint(state)}.");
				}

				if (state is not null)
				{
					return Refused($"'{prefabId}' is not an item definition, so it takes no state argument — an entity's creation-time values are the game's own.");
				}

				return SummonEntity(prefabId, x, y);
			}

			private WorldSummonOutcome SummonItem(string prefabId, SnbtValue? state, float x, float y)
			{
				if (!_grant.TryCreate(prefabId, state, out var item, out var refusal) || item is null)
				{
					return Refused(refusal ?? "");
				}

				if (!_worldItems.TryCreateAt(new Vector3(x, y, 0f), item, "Summon", out var createRefusal))
				{
					return Refused(createRefusal ?? "");
				}

				var stateText = state is null ? "" : SnbtWriter.Write(ItemStateDocument.Describe(item));
				return new WorldSummonOutcome(true, item.InstanceId, stateText, null);
			}

			private WorldSummonOutcome SummonEntity(string prefabId, float x, float y)
			{
				var created = RuntimeEntityFactory.TryCreate(prefabId, new Vector2(x, y), _log, "Summon");
				if (created == null)
				{
					return Refused("no prefab");
				}

				if (string.IsNullOrEmpty(created.id))
				{
					Object.Destroy(created.gameObject);
					return Refused($"the prefab '{prefabId}' carries no entity id, so no peer could recreate it. Nothing was created.");
				}

				return new WorldSummonOutcome(true, 0, "", null);
			}
		}
		""";
}
