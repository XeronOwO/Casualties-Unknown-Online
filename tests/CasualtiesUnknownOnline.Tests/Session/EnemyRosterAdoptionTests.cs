using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The enemy roster adoption judgment (EnemyRosterAdoption): the host's rows are
/// the identity, and the member's local copies are reused only when one is
/// plausibly the same animal. Every unmatched input is reported as an action —
/// materialize a row, destroy a surplus copy, or name a row that cannot be
/// identified yet — so no single drift can fail a whole set. The matrix here is
/// the per-row replacement for the rejected all-or-nothing pairing cases.
/// </summary>
public class EnemyRosterAdoptionTests
{
	private static NetworkEntityId Id(uint counter) => new(1, counter, 0);

	private static EnemyRosterAdoption.RosterRow Row(
		uint counter, string prefab, float hintX, float hintY, float? positionX = null, float? positionY = null, RuntimeEntityKey? key = null) =>
		new(
			Id(counter),
			prefab,
			new NetVector2(hintX, hintY),
			new NetVector2(positionX ?? hintX, positionY ?? hintY),
			key);

	private static EnemyRosterAdoption.LocalCopy Copy(int index, string prefab, float x, float y, RuntimeEntityKey? key = null) =>
		new(index, prefab, new NetVector2(x, y), key);

	[Fact]
	public void Plan_ReusesTheNearestSamePrefabCopy_AndDestroysTheFarOne()
	{
		var plan = EnemyRosterAdoption.Plan(
			[Row(1, "cavetick", 10f, 20f)],
			[Copy(0, "cavetick", 40f, 20f), Copy(1, "cavetick", 10.5f, 20f)]);

		var pair = Assert.Single(plan.Pairs);
		Assert.Equal(0, pair.RowIndex);
		Assert.Equal(1, pair.CopyIndex); // the copy within the reuse radius
		Assert.Equal(0.5f, pair.Distance, 3);
		Assert.Empty(plan.MaterializeRows);
		Assert.Equal([0], plan.DestroyCopies); // the far copy is surplus
		Assert.Empty(plan.Unresolved);
	}

	[Fact]
	public void Plan_RowWithNoCopyInsideTheRadius_IsMaterialized_AndTheDriftedCopyDestroyed()
	{
		// The reference case of the rejected scheme: one animal drifted far before
		// the session began. The row is materialized at the host's position and the
		// drifted local copy is destroyed — no whole-set verdict, no session-long gap.
		var plan = EnemyRosterAdoption.Plan(
			[Row(1, "cavetick", 10f, 20f)],
			[Copy(0, "cavetick", 27f, 20f)]);

		Assert.Empty(plan.Pairs);
		Assert.Equal([0], plan.MaterializeRows);
		Assert.Equal([0], plan.DestroyCopies);
	}

	[Fact]
	public void Plan_RadiusIsInclusiveAtTheBoundary()
	{
		var plan = EnemyRosterAdoption.Plan(
			[Row(1, "cavetick", 0f, 0f)],
			[Copy(0, "cavetick", EnemyRosterAdoption.MatchRadius, 0f)]);

		var pair = Assert.Single(plan.Pairs);
		Assert.Equal(EnemyRosterAdoption.MatchRadius, pair.Distance, 3);
		Assert.Empty(plan.MaterializeRows);
	}

	[Fact]
	public void Plan_DifferentPrefabAtTheSameSpot_IsNeitherReusedNorKept()
	{
		var plan = EnemyRosterAdoption.Plan(
			[Row(1, "cavetick", 10f, 20f)],
			[Copy(0, "shadecrawler", 10f, 20f)]);

		Assert.Empty(plan.Pairs);
		Assert.Equal([0], plan.MaterializeRows);
		Assert.Equal([0], plan.DestroyCopies);
	}

	[Fact]
	public void Plan_EmptyRoster_DestroysEveryLocalCopy()
	{
		// An empty host roster is a fact, not a no-op: the member's own generated
		// enemies are a different world and must not survive it.
		var plan = EnemyRosterAdoption.Plan(
			[],
			[Copy(0, "cavetick", 1f, 1f), Copy(1, "shadecrawler", 2f, 2f)]);

		Assert.Empty(plan.Pairs);
		Assert.Empty(plan.MaterializeRows);
		Assert.Equal([0, 1], plan.DestroyCopies);
		Assert.Empty(plan.Unresolved);
	}

	[Fact]
	public void Plan_NoLocalCopies_MaterializesEveryPrefabRow()
	{
		var plan = EnemyRosterAdoption.Plan(
			[Row(1, "cavetick", 1f, 1f), Row(2, "shadecrawler", 2f, 2f)],
			[]);

		Assert.Empty(plan.Pairs);
		Assert.Equal([0, 1], plan.MaterializeRows);
		Assert.Empty(plan.DestroyCopies);
	}

	[Fact]
	public void Plan_MissingPrefabRow_IsUnresolved_NotMaterialized()
	{
		// The prefab comes from the kernel fact; until it reaches this member the
		// row cannot be identified. The caller reports it, retries on the next
		// snapshot and — because the roster is incomplete — destroys nothing.
		var plan = EnemyRosterAdoption.Plan(
			[Row(1, "", 0f, 0f)],
			[Copy(0, "cavetick", 0f, 0f)]);

		var unresolved = Assert.Single(plan.Unresolved);
		Assert.Equal(0, unresolved.RowIndex);
		Assert.Equal(EnemyRosterAdoption.UnresolvedReason.MissingPrefab, unresolved.Reason);
		Assert.Empty(plan.Pairs);
		Assert.Empty(plan.MaterializeRows);
		Assert.Equal([0], plan.DestroyCopies);
	}

	[Fact]
	public void Plan_OneUnresolvedRow_DoesNotBlockTheOtherRows()
	{
		var plan = EnemyRosterAdoption.Plan(
			[Row(1, "", 0f, 0f), Row(2, "cavetick", 10f, 10f)],
			[Copy(0, "cavetick", 10f, 10f)]);

		var pair = Assert.Single(plan.Pairs);
		Assert.Equal(1, pair.RowIndex);
		var unresolved = Assert.Single(plan.Unresolved);
		Assert.Equal(0, unresolved.RowIndex);
		Assert.Empty(plan.MaterializeRows);
		Assert.Empty(plan.DestroyCopies);
	}

	[Fact]
	public void Plan_CreationKey_BeatsPosition()
	{
		// A row the host attributed to a creation record binds the copy carrying
		// the SAME key even when another same-prefab copy is nearer.
		var key = new RuntimeEntityKey("cavetick", 0, 0, 2001, 3);
		var plan = EnemyRosterAdoption.Plan(
			[Row(1, "cavetick", 0f, 0f, key: key)],
			[Copy(0, "cavetick", 0.1f, 0f), Copy(1, "cavetick", 100f, 0f, key)]);

		var pair = Assert.Single(plan.Pairs);
		Assert.Equal(1, pair.CopyIndex);
		Assert.Equal(0f, pair.Distance);
		Assert.Equal([0], plan.DestroyCopies); // the nearer keyless copy is a different animal
	}

	[Fact]
	public void Plan_KeyedRowWithNoKeyedCopy_IsMaterialized_NotPositionallyAbsorbed()
	{
		var key = new RuntimeEntityKey("cavetick", 0, 0, 2001, 3);
		var plan = EnemyRosterAdoption.Plan(
			[Row(1, "cavetick", 0f, 0f, key: key)],
			[Copy(0, "cavetick", 0f, 0f)]);

		Assert.Empty(plan.Pairs);
		Assert.Equal([0], plan.MaterializeRows);
		Assert.Equal([0], plan.DestroyCopies);
	}

	[Fact]
	public void Plan_KeylessRow_NeverConsumesAKeyedCopy()
	{
		var key = new RuntimeEntityKey("cavetick", 0, 0, 2001, 3);
		var plan = EnemyRosterAdoption.Plan(
			[Row(1, "cavetick", 0f, 0f)],
			[Copy(0, "cavetick", 0f, 0f, key)]);

		Assert.Empty(plan.Pairs);
		Assert.Equal([0], plan.MaterializeRows);
		Assert.Equal([0], plan.DestroyCopies);
	}

	[Fact]
	public void Plan_OneCopyServesOneRow()
	{
		var plan = EnemyRosterAdoption.Plan(
			[Row(1, "cavetick", 0f, 0f), Row(2, "cavetick", 0.1f, 0f)],
			[Copy(0, "cavetick", 0f, 0f)]);

		var pair = Assert.Single(plan.Pairs);
		Assert.Equal(0, pair.RowIndex); // the deterministic (hint x, hint y) order claims first
		Assert.Equal([1], plan.MaterializeRows);
		Assert.Empty(plan.DestroyCopies);
	}

	[Fact]
	public void Plan_EquidistantCopies_PickTheSameCopyRegardlessOfInputOrder()
	{
		// A tie must not depend on the caller's candidate order (the adapter's
		// tracked set has no guaranteed order): the plan orders copies by position.
		var row = Row(1, "cavetick", 5f, 5f);
		var left = Copy(0, "cavetick", 4f, 5f);
		var right = Copy(1, "cavetick", 6f, 5f);

		var firstCopies = new[] { left, right };
		var secondCopies = new[] { right, left };
		var first = EnemyRosterAdoption.Plan([row], firstCopies);
		var second = EnemyRosterAdoption.Plan([row], secondCopies);

		Assert.Equal(4f, firstCopies[first.Pairs.Single().CopyIndex].Position.X);
		Assert.Equal(4f, secondCopies[second.Pairs.Single().CopyIndex].Position.X);
	}

	[Theory]
	[InlineData(true, false, true)] // created during play — a runtime spawn
	[InlineData(true, true, false)] // Start ran during generation — generation output
	[InlineData(false, false, false)] // the world as it already stood before the session
	[InlineData(false, true, false)]
	public void IsRuntimeSpawn_OnlyDuringPlayInASession(bool sessionActive, bool generating, bool expected) =>
		Assert.Equal(expected, EnemyRosterAdoption.IsRuntimeSpawn(sessionActive, generating));

	[Theory]
	[InlineData(true, false, false, true)] // a live, settled world — the roster is complete
	[InlineData(true, true, false, false)] // generating — the host has not captured its whole set
	[InlineData(true, false, true, false)] // the regeneration fade — the scene is being replaced
	[InlineData(false, false, false, false)] // no world — nothing authoritative to publish
	[InlineData(false, true, false, false)]
	public void CanPublishRoster_OnlyOverALiveSettledWorld(bool hasWorld, bool generating, bool regenerating, bool expected) =>
		Assert.Equal(expected, EnemyRosterAdoption.CanPublishRoster(hasWorld, generating, regenerating));

	[Fact]
	public void FindLiveCopy_KnownKey_BindsOnlyTheKeyedCopy_RegardlessOfDistance()
	{
		var key = new RuntimeEntityKey("cavetick", 0, 0, 2001, 3);
		var copies = new[]
		{
			Copy(0, "cavetick", 0f, 0f),
			Copy(1, "cavetick", 100f, 0f, key),
		};

		var index = EnemyRosterAdoption.FindLiveCopy(
			new NetVector2(0f, 0f), "cavetick", key, allowKeyedCopy: true, copies, new bool[2]);

		Assert.Equal(1, index);
	}

	[Fact]
	public void FindLiveCopy_KnownKey_WithNoKeyedCopy_BindsNothing()
	{
		// A row with a known creation identity must never be handed a different
		// same-prefab copy: the snapshot's keyed materialization is the right end.
		var key = new RuntimeEntityKey("cavetick", 0, 0, 2001, 3);
		var copies = new[] { Copy(0, "cavetick", 0f, 0f) };

		var index = EnemyRosterAdoption.FindLiveCopy(
			new NetVector2(0f, 0f), "cavetick", key, allowKeyedCopy: true, copies, new bool[1]);

		Assert.Equal(-1, index);
	}

	[Fact]
	public void FindLiveCopy_GeneratedRow_NeverTakesAKeyedCopy()
	{
		var key = new RuntimeEntityKey("cavetick", 0, 0, 2001, 3);
		var copies = new[]
		{
			Copy(0, "cavetick", 0f, 0f, key),
			Copy(1, "cavetick", 0.2f, 0f),
		};

		var index = EnemyRosterAdoption.FindLiveCopy(
			new NetVector2(0f, 0f), "cavetick", creationKey: null, allowKeyedCopy: false, copies, new bool[2]);

		Assert.Equal(1, index); // the unkeyed copy, even though the keyed one is nearer
	}

	[Fact]
	public void FindLiveCopy_RuntimeRowWithNoKnownKey_MayBindAKeyedCopyProvisionally()
	{
		var key = new RuntimeEntityKey("cavetick", 0, 0, 2001, 3);
		var copies = new[] { Copy(0, "cavetick", 0f, 0f, key) };

		var index = EnemyRosterAdoption.FindLiveCopy(
			new NetVector2(0f, 0f), "cavetick", creationKey: null, allowKeyedCopy: true, copies, new bool[1]);

		Assert.Equal(0, index);
	}

	[Fact]
	public void FindLiveCopy_NearestSamePrefabInsideTheRadius_AndClaimedCopiesAreSkipped()
	{
		var copies = new[]
		{
			Copy(0, "cavetick", 10f, 0f),
			Copy(1, "cavetick", 10.4f, 0f),
			Copy(2, "shadecrawler", 10f, 0f),
		};
		var claimed = new[] { false, false, false };

		Assert.Equal(0, EnemyRosterAdoption.FindLiveCopy(new NetVector2(10f, 0f), "cavetick", null, false, copies, claimed));

		claimed[0] = true;
		Assert.Equal(1, EnemyRosterAdoption.FindLiveCopy(new NetVector2(10f, 0f), "cavetick", null, false, copies, claimed));
	}

	[Fact]
	public void FindLiveCopy_OutsideTheRadius_BindsNothing()
	{
		var copies = new[] { Copy(0, "cavetick", EnemyRosterAdoption.MatchRadius + 0.1f, 0f) };

		Assert.Equal(-1, EnemyRosterAdoption.FindLiveCopy(new NetVector2(0f, 0f), "cavetick", null, false, copies, new bool[1]));
	}
}
