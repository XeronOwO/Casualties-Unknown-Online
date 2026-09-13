using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The rule that decides whether a trap row reached the live world — the currency
/// the restore's live-write account counts (<c>EntityEventSync.OnTrapStateProjected</c>
/// feeds these booleans into <c>LiveWorldWriteOutcome</c>, which is what the
/// player's restore report names as refused). The Game Adapter reports what it
/// OBSERVED (was an entity there, what did the shared action return); this rule
/// decides what those observations mean, which is why it lives in the Runtime and
/// not in the adapter.
///
/// The case that used to be silent: the shared action library answered with a
/// bool, so "the local copy already carries this state" (a duplicate — the state
/// IS in the world) and "this copy cannot carry the fact at all" (a divergence —
/// e.g. a restored CrystalMimicTriggered row landing on a crystal that carries no
/// mimic effect) were THE SAME answer, and the second counted as restored.
/// </summary>
public class TrapActionVerdictTests
{
	[Fact]
	public void AnAppliedRow_ReachedTheWorld() =>
		Assert.True(
			TrapActionVerdict.ReachedTheLiveWorld(TrapActionOutcome.Applied),
			"a row the action applied is in the world");

	[Fact]
	public void ARowTheLocalCopyAlreadyCarried_ReachedTheWorld() =>
		Assert.True(
			TrapActionVerdict.ReachedTheLiveWorld(TrapActionOutcome.AlreadyInState),
			"a duplicate is not a lost row: the state the row names IS in the world");

	/// <summary>The recorded gap, named: the crystal IS there (the position key
	/// found it) and the restored mimic fact still exists nowhere.</summary>
	[Fact]
	public void ACopyThatCannotCarryTheFact_IsRefusedEvenThoughTheEntityExists() =>
		Assert.True(
			!TrapActionVerdict.ReachedTheLiveWorld(TrapActionOutcome.NotApplicable),
			"an entity that exists but cannot carry the fact must be a REFUSED row the restore report names, never a restored one");

	[Fact]
	public void ARowWithNoEntityAtAll_IsRefused() =>
		Assert.True(
			!TrapActionVerdict.ReachedTheLiveWorld(null),
			"the regenerated layer does not have the entity, so the fact exists nowhere");

	/// <summary>The adversarial review's fail-open finding: a verdict the rule does
	/// not know must be REFUSED, never counted as reached. Otherwise a follow-up that
	/// adds an outcome (say "superseded") counts the row as restored while every log
	/// arm says it is not in the world — the exact under-report this change closes.</summary>
	[Theory]
	[InlineData(99)]
	[InlineData(-1)]
	public void AVerdictTheRuleDoesNotKnow_IsRefused(int undeclared) =>
		Assert.True(
			!TrapActionVerdict.ReachedTheLiveWorld((TrapActionOutcome)undeclared),
			$"an undeclared verdict ({(TrapActionOutcome)undeclared}) must fail CLOSED — refused, not counted as restored");

	/// <summary>The same finding, one level up: every DECLARED outcome carries a
	/// deliberate verdict, so adding a member forces the classification instead of
	/// silently inheriting the fail-closed default (the repo's table-pin idiom, as
	/// in `EntityEventProfilesTests`).</summary>
	[Fact]
	public void EveryDeclaredOutcome_CarriesADeliberateVerdict()
	{
		var expected = new Dictionary<TrapActionOutcome, bool>
		{
			[TrapActionOutcome.Applied] = true,
			[TrapActionOutcome.AlreadyInState] = true,
			[TrapActionOutcome.NotApplicable] = false,
		};

		Assert.Equal(Enum.GetValues(typeof(TrapActionOutcome)).Length, expected.Count);
		foreach (var row in expected)
		{
			Assert.True(
				TrapActionVerdict.ReachedTheLiveWorld(row.Key) == row.Value,
				$"{row.Key} must be classified as reached={row.Value}");
		}
	}
}
