using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The row-level containment below decision 175: a restored cut's writes are row loops, and
/// one row that reaches an engine call the local copy cannot serve must cost ITSELF — the
/// rows behind it still land, and the account carries an exact refused count instead of
/// "the write threw". The rule lives in the Runtime (where the account is machine-checkable
/// without a game); the Game Adapter's loops call it with each row's own identity, and
/// those call sites are read-only reviewed because their bodies are game-typed.
/// </summary>
public class ContainedRowLoopTests
{
	private static readonly string[] Rows = ["a", "b", "c", "d"];

	[Fact]
	public void Run_AThrowingRowIsRefusedAndTheRowsBehindItStillLand()
	{
		var log = new RecordingLogger<ContainedRowLoopTests>();
		var applied = new List<string>();

		var outcome = ContainedRowLoop.Run(
			Rows,
			row =>
			{
				if (row == "b")
				{
					throw new InvalidOperationException("row b reached an engine call the local copy cannot serve");
				}

				applied.Add(row);
				return true;
			},
			row => $"row {row}",
			log,
			"Restored trap");

		Assert.Equal(3, outcome.Applied);
		Assert.Equal(1, outcome.Refused);
		Assert.Equal<string>(["a", "c", "d"], applied);

		// The log names the ROW, not the loop: the identity is what a reader needs to find the
		// copy the local world could not serve.
		Assert.True(log.HasError("row b"));
	}

	[Fact]
	public void Run_ARowTheWorldDoesNotTake_IsRefusedWithoutAnErrorLine()
	{
		// The appliers already log their own "no entity there" warning, so a plain refusal is
		// their words, not the containment's: only a THROW is an unexpected refusal here.
		var log = new RecordingLogger<ContainedRowLoopTests>();

		var outcome = ContainedRowLoop.Run(
			Rows,
			row => row != "c",
			row => $"row {row}",
			log,
			"Restored trap");

		Assert.Equal(3, outcome.Applied);
		Assert.Equal(1, outcome.Refused);
		Assert.Empty(log.Entries);
	}

	[Fact]
	public void Run_WhenEveryRowThrows_TheOutcomeIsStillExact()
	{
		var log = new RecordingLogger<ContainedRowLoopTests>();

		var outcome = ContainedRowLoop.Run(
			Rows,
			_ => throw new InvalidOperationException("no entity there"),
			row => $"row {row}",
			log,
			"Restored trap");

		Assert.Equal(0, outcome.Applied);
		Assert.Equal(Rows.Length, outcome.Refused);

		// Every row was attempted exactly once (the throw of one did not end the loop), and
		// every line the containment wrote is an error.
		Assert.Equal(Rows.Length, log.Entries.Count);
		Assert.All(log.Entries, entry => Assert.Equal(LogLevel.Error, entry.Level));
	}

	[Fact]
	public void Run_NoRows_NeverCallsTheApplier()
	{
		// Vacuous if it only asserts the zero outcome: the applier must not be reached either,
		// or "no rows" would be indistinguishable from "a row was attempted and lost".
		var log = new RecordingLogger<ContainedRowLoopTests>();

		var outcome = ContainedRowLoop.Run<string>(
			[],
			_ => throw new InvalidOperationException("there is no row to apply"),
			_ => "row",
			log,
			"Restored trap");

		Assert.Equal(0, outcome.Applied);
		Assert.Equal(0, outcome.Refused);
		Assert.Empty(log.Entries);
	}

	/// <summary>
	/// The other shape of the rule: an applier that keeps its OWN accounting (written /
	/// unchanged / refused by its own rule) gets back only the THROWN count, which it adds to
	/// its refused total. The rows behind a throwing row still run.
	/// </summary>
	[Fact]
	public void RunContained_ReturnsTheThrownCountAndKeepsGoing()
	{
		var log = new RecordingLogger<ContainedRowLoopTests>();
		var reached = new List<string>();

		var thrown = ContainedRowLoop.RunContained(
			Rows,
			row =>
			{
				reached.Add(row);
				if (row is "a" or "c")
				{
					throw new InvalidOperationException("this row's engine call cannot be served");
				}
			},
			row => $"row {row}",
			log,
			"Partial-damage");

		Assert.Equal(2, thrown);
		Assert.Equal<string>(["a", "b", "c", "d"], reached);
		Assert.True(log.HasError("row a"));
		Assert.True(log.HasError("row c"));
	}

	/// <summary>
	/// A systematic failure — every row throwing, the shape decision 175 exists for — is
	/// bounded: the first rows are named, the tail is counted in exactly one line, and the
	/// refused total stays exact.
	/// </summary>
	[Fact]
	public void RunContained_ASystematicFailureNamesTheHeadAndSummarisesTheTail()
	{
		var log = new RecordingLogger<ContainedRowLoopTests>();
		var rows = new string[9];

		var thrown = ContainedRowLoop.RunContained(
			rows,
			_ => throw new InvalidOperationException("the component was never initialized"),
			row => $"row {Array.IndexOf(rows, row)}",
			log,
			"Restored trap");

		Assert.Equal(9, thrown);

		// Five named rows plus ONE summary — not nine stack traces.
		Assert.Equal(6, log.Entries.Count);
		Assert.All(log.Entries, entry => Assert.Equal(LogLevel.Error, entry.Level));
		Assert.Contains("4 further row(s)", log.Entries[5].Message, StringComparison.Ordinal);
		Assert.Contains("9 refused in all", log.Entries[5].Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// The third shape: the loop's unit is a LIVE WORLD OBJECT (the keypad and geyser tables
	/// iterate what the world holds and count the RESTORED rows they matched), so the object's
	/// identity is its position and the caller keeps its own count — incremented only once the
	/// matched row's write path completed.
	/// </summary>
	[Fact]
	public void RunLiveWorld_AThrowingObjectCostsOnlyItselfAndIsNamed()
	{
		var log = new RecordingLogger<ContainedRowLoopTests>();
		var reached = new List<string>();
		var applied = 0;

		ContainedRowLoop.RunLiveWorld(
			Rows,
			live =>
			{
				reached.Add(live);
				if (live == "b")
				{
					throw new InvalidOperationException("the object's component cannot be served");
				}

				applied++;
			},
			live => $"object at ({live})",
			log,
			"restored keypad code");

		// Every object was attempted exactly once, and the count the caller keeps is the one the
		// restore's account reads: the throwing object is simply absent from it.
		Assert.Equal<string>(["a", "b", "c", "d"], reached);
		Assert.Equal(3, applied);
		Assert.Equal(1, Rows.Length - applied); // the caller's refusal count: (rows - applied)
		Assert.True(log.HasError("object at (b)"));
	}

	[Fact]
	public void RunLiveWorld_NoThrow_AppliesEveryObjectOnceAndWritesNothing()
	{
		// The ticket's "every count is identical to today's" row: with no throw the loop reaches
		// every object exactly once and the containment contributes no log line at all.
		var log = new RecordingLogger<ContainedRowLoopTests>();
		var applied = 0;

		ContainedRowLoop.RunLiveWorld(Rows, _ => applied++, live => $"object {live}", log, "restored geyser type");

		Assert.Equal(Rows.Length, applied);
		Assert.Empty(log.Entries);
	}

	[Fact]
	public void RunLiveWorld_ASystematicFailureIsBoundedLikeTheRowRule()
	{
		var log = new RecordingLogger<ContainedRowLoopTests>();
		var live = new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8" };

		ContainedRowLoop.RunLiveWorld(
			live,
			_ => throw new InvalidOperationException("the component was never initialized"),
			item => $"object at ({item})",
			log,
			"restored keypad code");

		// Five named objects plus ONE summary — the same bound the row rule applies, so a
		// systematic failure cannot replace an exact count with a flood of stack traces.
		Assert.Equal(6, log.Entries.Count);
		Assert.All(log.Entries, entry => Assert.Equal(LogLevel.Error, entry.Level));
		Assert.True(log.HasError("object at (0)"));
		Assert.True(log.HasError("object at (4)"));
		Assert.Contains("4 further row(s)", log.Entries[5].Message, StringComparison.Ordinal);
		Assert.Contains("9 refused in all", log.Entries[5].Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// The identity is the ADAPTER's knowledge, and a live object's identity is read off the very
	/// object whose engine call just threw (its position). Naming a refusing unit must therefore
	/// never be able to throw in turn: that would replace the original failure with its own and
	/// let the exception escape the containment — the exact outcome the rule exists to prevent.
	/// </summary>
	[Fact]
	public void RunContained_AnIdentityThatThrowsCannotReplaceTheOriginalFailure()
	{
		var log = new RecordingLogger<ContainedRowLoopTests>();
		var reached = new List<string>();

		var thrown = ContainedRowLoop.RunContained(
			Rows,
			row =>
			{
				reached.Add(row);
				throw new InvalidOperationException($"the write for {row} threw");
			},
			row =>
			{
				if (row == "a")
				{
					throw new InvalidOperationException("the position could not be read");
				}

				return $"row {row}";
			},
			log,
			"restored keypad code");

		// Every row was still attempted exactly once, every failure still produced its error
		// line, and the one whose identity could not be produced is reported as unknown instead
		// of aborting the loop.
		Assert.Equal(Rows.Length, thrown);
		Assert.Equal<string>(["a", "b", "c", "d"], reached);
		Assert.Equal(Rows.Length, log.Entries.Count);
		Assert.True(log.HasError("<identity unavailable>"));
		Assert.True(log.HasError("row b"));
	}
}
