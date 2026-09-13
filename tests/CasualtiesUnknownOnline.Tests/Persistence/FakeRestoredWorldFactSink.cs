using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// In-memory stand-in for the Game Adapter's live-world writes: it records the
/// calls in order and models the game's own partial-damage list as the rows that
/// survive the last replace, so a replay suite can assert what the live world
/// would hold instead of only what it was handed.
/// </summary>
internal sealed class FakeRestoredWorldFactSink : IRestoredWorldFactSink
{
	/// <summary>The world's readiness the replay must respect (a mid-generation world is not ready).</summary>
	internal bool WorldReady { get; set; } = true;

	/// <summary>Simulate the world vanishing BETWEEN the readiness check and the write (the two are not atomic in production).</summary>
	internal bool RefuseBlockWrites { get; set; }

	/// <summary>
	/// Simulate an ENGINE call throwing mid-write (a mod tile index the game cannot
	/// resolve, a Traverse on a component type that changed): the replay must report
	/// the loss and release both handovers instead of leaking the exception into the
	/// pump and leaving the rows armed for the next generation.
	/// </summary>
	internal bool ThrowOnBlockWrite { get; set; }

	/// <summary>Simulate a WORLD-ENTITY ROW throwing (the trap replay's one shared action
	/// reaching an engine call the local copy cannot serve): the write that threw is the
	/// entity half's, and the halves that had already landed must keep their accounting
	/// instead of being reported as not taken — see <c>RestoredWorldFactReplayTests</c>.
	/// </summary>
	internal bool ThrowOnWorldEntityWrite { get; set; }

	/// <summary>Every call this sink received, in order.</summary>
	internal List<string> Calls { get; } = [];

	internal List<BlockStateEntryMsg> WrittenBlockStates { get; } = [];

	/// <summary>The game's own damage list as it stands: emptied by every replace, appended by the write.</summary>
	internal List<BlockDamageEntryMsg> GameDamageTable { get; } = [];

	/// <summary>The game's own list cap (<c>WorldGeneration.cs:732-737</c> caps it at 128); a write past it is refused, exactly like the production table.</summary>
	internal int Capacity { get; set; } = 128;

	/// <summary>How many keypad codes the live world has no Openable for.</summary>
	internal int RefuseKeypads { get; set; }

	/// <summary>How many geyser entries the live world has no geyser for.</summary>
	internal int RefuseGeysers { get; set; }

	/// <summary>How many recipe unlock rows name a recipe this world's table does not have (a mod update removed it).</summary>
	internal int RefuseRecipes { get; set; }

	/// <summary>The recipe unlock state the live world ended up with — the rows this sink was handed and took.</summary>
	internal List<SaveRecipeUnlockRow> AppliedRecipes { get; } = [];

	/// <summary>Whether the live world had a radiation line to write.</summary>
	internal bool RadiationLinePresent { get; set; } = true;

	internal int AppliedKeypads { get; private set; }

	internal int AppliedGeysers { get; private set; }

	public bool IsWorldReady => WorldReady;

	public LiveWorldWriteOutcome WriteBlockStates(IReadOnlyList<BlockStateEntryMsg> states)
	{
		Calls.Add("write-block-states");
		if (ThrowOnBlockWrite)
		{
			throw new InvalidOperationException("the world refused to take a restored block row");
		}

		if (!WorldReady || RefuseBlockWrites)
		{
			// The readiness check and the write are not atomic in production either:
			// a world that vanished refuses every row instead of reporting zero rows.
			return new LiveWorldWriteOutcome(0, states.Count);
		}

		WrittenBlockStates.AddRange(states);
		return LiveWorldWriteOutcome.All(states.Count);
	}

	public LiveWorldWriteOutcome ReplaceGameBlockDamages(IReadOnlyList<BlockDamageEntryMsg> rows)
	{
		Calls.Add("replace-game-damages");
		GameDamageTable.Clear();
		if (!WorldReady)
		{
			return new LiveWorldWriteOutcome(0, rows.Count);
		}

		var applied = 0;
		var refused = 0;
		foreach (var row in rows)
		{
			if (GameDamageTable.Count >= Capacity)
			{
				refused++;
				continue;
			}

			GameDamageTable.Add(row);
			applied++;
		}

		return new LiveWorldWriteOutcome(applied, refused);
	}

	public LiveWorldWriteOutcome ApplyKeypadCodes(IReadOnlyList<KeypadEntryMsg> codes)
	{
		Calls.Add("apply-keypads");
		var refused = Math.Min(RefuseKeypads, codes.Count);
		AppliedKeypads = codes.Count - refused;
		return new LiveWorldWriteOutcome(AppliedKeypads, refused);
	}

	public LiveWorldWriteOutcome ApplyGeysers(IReadOnlyList<GeyserStateEntryMsg> geysers)
	{
		Calls.Add("apply-geysers");
		var refused = Math.Min(RefuseGeysers, geysers.Count);
		AppliedGeysers = geysers.Count - refused;
		return new LiveWorldWriteOutcome(AppliedGeysers, refused);
	}

	public LiveWorldWriteOutcome ApplyRecipeUnlocks(IReadOnlyList<SaveRecipeUnlockRow> recipes)
	{
		Calls.Add("apply-recipes");
		var refused = Math.Min(RefuseRecipes, recipes.Count);
		for (var i = 0; i < recipes.Count - refused; i++)
		{
			AppliedRecipes.Add(recipes[i]);
		}

		return new LiveWorldWriteOutcome(recipes.Count - refused, refused);
	}

	public bool ApplyRadiationLine(RadiationLineStateMsg line)
	{
		Calls.Add("apply-radiation");
		return RadiationLinePresent;
	}

	/// <summary>Simulate a regenerated layer that does not hold the entities the cut recorded: every world-entity row is refused.</summary>
	internal bool RefuseWorldEntities { get; set; }

	/// <summary>The restored world-entity facts this sink was handed (one entry per write that carried any).</summary>
	internal List<RestoredWorldEntityFacts> WrittenWorldEntities { get; } = [];

	public LiveWorldWriteOutcome ApplyWorldEntities(RestoredWorldEntityFacts facts)
	{
		Calls.Add("apply-world-entities");
		if (ThrowOnWorldEntityWrite)
		{
			throw new InvalidOperationException("a restored world-entity row reached an engine call it cannot serve");
		}

		if (!WorldReady || RefuseWorldEntities)
		{
			return new LiveWorldWriteOutcome(0, facts.Count);
		}

		if (facts.Count > 0)
		{
			WrittenWorldEntities.Add(facts);
		}

		return LiveWorldWriteOutcome.All(facts.Count);
	}
}
