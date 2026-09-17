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
}
