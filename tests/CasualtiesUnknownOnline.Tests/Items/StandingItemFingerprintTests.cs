using System;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Tests.Patching;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The standing materializer's reconcile gate: one owner's carried tree read BY REFERENCE, so a fire of the
/// carried-fact edge that left every list and entry instance in place costs one walk instead of a plan
/// rebuild and a reflection-backed digest of every row (the edge also fires for enemy/medical/limb events).
///
/// <para>
/// The tree is what makes the cost argument hold, so both directions are pinned: every shape the fact table
/// really produces (a replaced element, an added or removed one, a changed or added nested content, a rebuilt
/// list) must FAIL the comparison — erring towards a reconcile — and the one shape it cannot see (an in-place
/// field write to a stored element, which two host-side writers do) is pinned as the known blind spot with
/// the fresh 1 Hz snapshot named as the safety net that bounds it to one interval.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class StandingItemFingerprintTests
{
	private static readonly Type Fingerprint = GameAssemblyHost.Adapter
		.GetType("CasualtiesUnknownOnline.GameAdapter.Items.StandingItemFingerprint", throwOnError: true)!;

	private static readonly MethodInfo Of = Fingerprint
		.GetMethod("Of", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("StandingItemFingerprint.Of not found.");

	private static readonly MethodInfo Matches = Fingerprint
		.GetMethod("Matches", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("StandingItemFingerprint.Matches not found.");

	[Fact]
	public void TheFingerprint_MatchesTheSameTree()
	{
		var items = new List<CharacterItemMsg> { Row(101, Row(102)), Row(103) };
		var fingerprint = Of.Invoke(null, [items])!;

		Assert.True(Decide(fingerprint, items), "the same list, the same elements and the same contents are the same tree");
	}

	[Fact]
	public void TheFingerprint_RefusesAReplacedElement()
	{
		var items = new List<CharacterItemMsg> { Row(101), Row(102) };
		var fingerprint = Of.Invoke(null, [items])!;

		// What the fact table does for a carried fact: the entry is REPLACED, not edited in place.
		items[1] = Row(102);

		Assert.False(Decide(fingerprint, items), "a replaced entry is a change the reconcile must see");
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void TheFingerprint_RefusesAnAddedOrRemovedElement(bool added)
	{
		var items = new List<CharacterItemMsg> { Row(101), Row(102) };
		var fingerprint = Of.Invoke(null, [items])!;

		if (added)
		{
			items.Add(Row(103));
		}
		else
		{
			items.RemoveAt(1);
		}

		Assert.False(Decide(fingerprint, items), "a pickup, a drop or a nested move changes the set of rows");
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void TheFingerprint_RefusesAChangedOrAddedNestedContent(bool added)
	{
		var items = new List<CharacterItemMsg> { Row(101, Row(102)) };
		var fingerprint = Of.Invoke(null, [items])!;

		if (added)
		{
			items[0].Contents.Add(Row(103));
		}
		else
		{
			items[0].Contents[0] = Row(102);
		}

		Assert.False(Decide(fingerprint, items), "the contents of a carried container are rows of their own — a change there must reconcile");
	}

	[Fact]
	public void TheFingerprint_RefusesARebuiltListOfTheSameElements()
	{
		var items = new List<CharacterItemMsg> { Row(101) };
		var fingerprint = Of.Invoke(null, [items])!;

		// A snapshot arrives as a fresh message and a fresh list: the comparison errs towards reconciling.
		var rebuilt = new List<CharacterItemMsg>(items);

		Assert.False(Decide(fingerprint, rebuilt), "a rebuilt list is not the tree the last reconcile saw, even with the same entries");
	}

	[Fact]
	public void TheFingerprint_AcceptsAnInPlaceFieldWrite_WhichIsWhyTheFreshSnapshotIsTheSafetyNet()
	{
		var items = new List<CharacterItemMsg> { Row(101) };
		var fingerprint = Of.Invoke(null, [items])!;

		// The known blind spot, pinned on purpose: two host-side writers (ItemArbitration.AdoptEvidence and
		// TransferTableRestoreMerge.TakeState) rewrite fields of a transferred/restored message that the fact
		// table stores without copying. The gate cannot see that — and does not have to, because the owner's
		// own 1 Hz report is deserialized fresh and replaces the message and its list every second, so the
		// full reconcile runs within one interval. A change that stops replacing the snapshot instance would
		// turn this from a delay into a gate that misses a change for good (StandingItemFingerprint's doc).
		items[0].Condition = 0.25f;

		Assert.True(Decide(fingerprint, items), "an in-place field write keeps every reference — the documented blind spot, bounded by the fresh snapshot");
	}

	private static CharacterItemMsg Row(ulong instanceId, params CharacterItemMsg[] contents) => new()
	{
		InstanceId = instanceId,
		ItemId = "trashbag",
		Condition = 1f,
		Contents = [.. contents],
	};

	private static bool Decide(object fingerprint, List<CharacterItemMsg> items) =>
		(bool)Matches.Invoke(fingerprint, [items])!;
}
