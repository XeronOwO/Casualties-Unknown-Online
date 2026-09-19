using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;

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

	/// <summary>
	/// The level BELOW <see cref="ThrowOnWorldEntityWrite"/>: the trap rows go through the same
	/// per-row containment the three adapter loops use (<see cref="ContainedRowLoop"/>), with the
	/// row at this index reaching an engine call the local copy cannot serve. The rows behind it
	/// still land, so the half's count names exactly ONE refused row instead of "the write threw".
	/// </summary>
	internal int ThrowOnWorldEntityRow { get; set; } = -1;

	/// <summary>The per-row log the containment writes to — the error line's row identity is asserted through it.</summary>
	internal RecordingLogger<FakeRestoredWorldFactSink> RowLog { get; } = new();

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

	/// <summary>
	/// Simulate a LIVE keypad object whose code field this copy cannot serve (the table iterates
	/// the live world and runs each object through the Runtime's per-object containment): the
	/// object at this index throws, the ones behind it still land, and the refused count the
	/// caller computes (rows - applied) names exactly ONE lost row.
	/// </summary>
	internal int ThrowOnKeypadObject { get; set; } = -1;

	/// <summary>The geyser half of <see cref="ThrowOnKeypadObject"/> — the same containment over the live geysers.</summary>
	internal int ThrowOnGeyserObject { get; set; } = -1;

	/// <summary>
	/// Simulate a RECIPE row whose index exists but whose write throws: the adapter table keeps
	/// its own accounting and adds the thrown rows as a refusal class of their own, so the seam
	/// must see an exact refused count instead of "the live-world write threw".
	/// </summary>
	internal int ThrowOnRecipeRow { get; set; } = -1;

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
		AppliedKeypads = ApplyLiveObjects(
			codes,
			RefuseKeypads,
			ThrowOnKeypadObject,
			code => $"({code.Position.X:F1},{code.Position.Y:F1})",
			"restored keypad code");
		return new LiveWorldWriteOutcome(AppliedKeypads, codes.Count - AppliedKeypads);
	}

	public LiveWorldWriteOutcome ApplyGeysers(IReadOnlyList<GeyserStateEntryMsg> geysers)
	{
		Calls.Add("apply-geysers");
		AppliedGeysers = ApplyLiveObjects(
			geysers,
			RefuseGeysers,
			ThrowOnGeyserObject,
			geyser => $"({geyser.Position.X:F1},{geyser.Position.Y:F1})",
			"restored geyser type");
		return new LiveWorldWriteOutcome(AppliedGeysers, geysers.Count - AppliedGeysers);
	}

	public LiveWorldWriteOutcome ApplyRecipeUnlocks(IReadOnlyList<SaveRecipeUnlockRow> recipes)
	{
		Calls.Add("apply-recipes");
		if (ThrowOnRecipeRow >= 0)
		{
			return ApplyRecipeRowsContained(recipes);
		}

		var refused = Math.Min(RefuseRecipes, recipes.Count);
		for (var i = 0; i < recipes.Count - refused; i++)
		{
			AppliedRecipes.Add(recipes[i]);
		}

		return new LiveWorldWriteOutcome(recipes.Count - refused, refused);
	}

	/// <summary>
	/// The two native tables iterate the LIVE world, run each object through the Runtime's
	/// per-object containment, and count a matched row only once its write path COMPLETED — so
	/// the refused count the caller reports is (rows - applied), and an object that threw is
	/// already inside it. This models that arithmetic, with <paramref name="unMatchedRows"/>
	/// standing for the restored rows the live world has no object for.
	/// </summary>
	private int ApplyLiveObjects<T>(
		IReadOnlyList<T> rows,
		int unMatchedRows,
		int throwAtIndex,
		Func<T, string> identity,
		string what)
	{
		var applied = 0;
		var index = -1;
		ContainedRowLoop.RunLiveWorld(
			rows,
			_ =>
			{
				index++;
				if (index == throwAtIndex)
				{
					throw new InvalidOperationException($"the live world's {what} object could not be served");
				}

				if (index < rows.Count - unMatchedRows)
				{
					applied++;
				}
			},
			identity,
			RowLog,
			what);
		return applied;
	}

	/// <summary>
	/// The recipe table keeps its own accounting (a missing index is refused by its rule) and
	/// runs its rows through the Runtime's per-row containment; a row that THREW is refused as
	/// its own class and the rows behind it still land — the arithmetic the adapter's
	/// <c>RecipeUnlockTable.Apply</c> performs.
	/// </summary>
	private LiveWorldWriteOutcome ApplyRecipeRowsContained(IReadOnlyList<SaveRecipeUnlockRow> recipes)
	{
		var refusedIndexes = 0;
		var applied = 0;
		var thrown = ContainedRowLoop.RunContained(
			recipes,
			row =>
			{
				if (row.Index < 0)
				{
					refusedIndexes++;
					return;
				}

				if (row.Index == ThrowOnRecipeRow)
				{
					throw new InvalidOperationException($"recipe row index {row.Index} reached an engine call the local copy cannot serve");
				}

				AppliedRecipes.Add(row);
				applied++;
			},
			row => $"index {row.Index}",
			RowLog,
			"restored recipe unlock");

		return new LiveWorldWriteOutcome(applied, refusedIndexes + thrown);
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

		if (ThrowOnWorldEntityRow >= 0)
		{
			return ApplyWorldEntitiesRowByRow(facts);
		}

		if (facts.Count > 0)
		{
			WrittenWorldEntities.Add(facts);
		}

		return LiveWorldWriteOutcome.All(facts.Count);
	}

	/// <summary>
	/// The three adapter loops run their rows through <see cref="ContainedRowLoop"/>; this models
	/// the FIRST of them (the trap replay) with one throwing row, so a suite can assert what the
	/// containment buys: the rows behind the throw still land, and the half's refused count is
	/// exact. The other two lists apply whole, which is what the suites that use this mode expect
	/// them to be handed (empty).
	/// </summary>
	private LiveWorldWriteOutcome ApplyWorldEntitiesRowByRow(RestoredWorldEntityFacts facts)
	{
		var index = -1;
		var traps = ContainedRowLoop.Run(
			facts.Traps,
			trap =>
			{
				index++;
				if (index == ThrowOnWorldEntityRow)
				{
					throw new InvalidOperationException($"trap row {index} reached an engine call the local copy cannot serve");
				}

				return true;
			},
			trap => $"{trap.Kind} at ({trap.Position.X:F1},{trap.Position.Y:F1})",
			RowLog,
			"restored trap");

		return new LiveWorldWriteOutcome(
			traps.Applied + facts.Opened.Count + facts.Health.Count,
			traps.Refused);
	}
}
