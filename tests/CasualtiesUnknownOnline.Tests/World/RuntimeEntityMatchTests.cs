using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The runtime entity-creation match judgment: which local BuildingEntity a
/// creation record binds to. Two passes — the record's own copy by exact
/// creation key, then a MARKERLESS same-prefab copy inside the 1 m radius
/// (trap-layout materializations, enemy-domain backfill copies and generated
/// entities never entered the runtime-creation tables). A candidate that
/// carries a marker is never a positional bind target: that is what swallowed a
/// second same-prefab creation inside one cell (round-3 finding 1).
/// </summary>
public class RuntimeEntityMatchTests
{
	private static RuntimeEntityKey Key(string id, int x, int y, ulong creator, uint sequence) => new(id, x, y, creator, sequence);

	private static RuntimeEntityMatch.Candidate Marked(string id, float x, float y, RuntimeEntityKey key, bool tutorial = false) =>
		new(id, x, y, tutorial, key);

	private static RuntimeEntityMatch.Candidate Markerless(string id, float x, float y, bool tutorial = false) =>
		new(id, x, y, tutorial, null);

	[Fact]
	public void FindIndex_BindsTheCandidateCarryingTheSameCreationKey()
	{
		var key = Key("keypad", 12, 34, 2001, 5);
		var candidates = new List<RuntimeEntityMatch.Candidate>
		{
			Markerless("keypad", 12.1f, 34.1f),
			Marked("keypad", 12.2f, 34.2f, key),
			Marked("keypad", 12.3f, 34.3f, Key("keypad", 12, 34, 2001, 6)), // the sibling creation in the same cell
		};

		Assert.Equal(1, RuntimeEntityMatch.FindIndex(candidates, key, 12f, 34f));
	}

	[Fact]
	public void FindIndex_TwoCreationsOfTheSamePrefabInOneCell_NeverShareACopy()
	{
		// The round-3 regression: two identical prefabs 0.7 m apart share the
		// floored cell. The sibling's copy carries a DIFFERENT key, so it is
		// never a positional bind target for this record.
		var first = Key("turret", 5, 7, 2001, 1);
		var second = Key("turret", 5, 7, 2001, 2);
		var candidates = new List<RuntimeEntityMatch.Candidate> { Marked("turret", 5.2f, 7.2f, first) };

		Assert.Equal(0, RuntimeEntityMatch.FindIndex(candidates, first, 5.2f, 7.2f));
		Assert.Equal(-1, RuntimeEntityMatch.FindIndex(candidates, second, 5.9f, 7.6f));
	}

	[Fact]
	public void FindIndex_BindsADriftedCopyByKey()
	{
		// A runtime creation's Rigidbody2D becomes Dynamic while its chunk is
		// visible, so the copy can be far outside the creation cell when a
		// re-report or a re-broadcast arrives. The key is the identity.
		var key = Key("keypad", 0, 0, 2001, 9);
		var candidates = new List<RuntimeEntityMatch.Candidate>
		{
			Marked("keypad", 40f, -12f, Key("keypad", 40, -12, 2001, 3)), // a DIFFERENT keypad, drifted far away
			Marked("keypad", 41f, -12f, key),
		};

		Assert.Equal(1, RuntimeEntityMatch.FindIndex(candidates, key, 0f, 0f));
	}

	[Fact]
	public void FindIndex_BindsAMarkerlessCopyInsideTheRadius()
	{
		// An enemy-domain backfill copy (EnemySyncCoordinator.CreateRuntimeSpawn)
		// carries no creation marker: it was materialized from the host's enemy
		// snapshot at the animal's CURRENT position. A live re-report of that
		// animal must bind it instead of creating a second animal, so the
		// positional pass stays for markerless copies. (Trap-layout
		// materializations no longer report at all — they carry a
		// SpawnReplayMarker.)
		var key = Key("cavetick", -13, 466, 2001, 1);
		var candidates = new List<RuntimeEntityMatch.Candidate> { Markerless("cavetick", -13f, 466.8f) };

		Assert.Equal(0, RuntimeEntityMatch.FindIndex(candidates, key, -13f, 466f));
	}

	[Fact]
	public void FindIndex_RejectsTheMarkerlessRadiusBoundary()
	{
		// Strictly inside: the boundary itself is not a match.
		var key = Key("spikestabber", 0, 0, 2001, 1);

		Assert.Equal(0, RuntimeEntityMatch.FindIndex([Markerless("spikestabber", 0.999f, 0f)], key, 0f, 0f));
		Assert.Equal(-1, RuntimeEntityMatch.FindIndex([Markerless("spikestabber", 1f, 0f)], key, 0f, 0f));
	}

	[Fact]
	public void FindIndex_NeverBindsAPerPlayerTutorialProp()
	{
		// The shared-domain record must not absorb another player's private
		// course object, even at the same prefab and position.
		var key = Key("keypad", 0, 0, 2001, 1);

		Assert.Equal(-1, RuntimeEntityMatch.FindIndex([Markerless("keypad", 0f, 0f, tutorial: true)], key, 0f, 0f));
	}

	[Fact]
	public void FindIndex_NeverBindsADifferentPrefab()
	{
		var key = Key("keypad", 0, 0, 2001, 1);

		Assert.Equal(-1, RuntimeEntityMatch.FindIndex([Markerless("landmine", 0.1f, 0f)], key, 0f, 0f));
	}

	[Fact]
	public void FindIndex_ADeferredGeyserReportBindsItsOwnCopyNotTheNearestSameCellOne()
	{
		// A geyser's liquid type is read from its CHILD's Start one frame after
		// the parent, so the deferred report carries the root creation key while
		// the child can sit in another cell. The locate must bind by that key —
		// the old 3 m FindTrap first hit bound the nearest same-cell geyser and
		// made the report key differ from the death key.
		var own = Key("geyser", 5, 7, 2001, 2);
		var candidates = new List<RuntimeEntityMatch.Candidate>
		{
			Marked("geyser", 5f, 7f, Key("geyser", 5, 7, 2001, 1)), // a DIFFERENT geyser right at the recorded position
			Marked("geyser", 7.5f, 7f, own),                        // our creation, its child 2.5 m away
		};

		Assert.Equal(1, RuntimeEntityMatch.FindIndex(candidates, own, 5f, 7f));
	}

	[Fact]
	public void FindIndex_ThreeSpawnedTurretsCloseTogether_EachBindsItsOwnCopy()
	{
		// The original three-turret regression, now resolved by identity for
		// marked copies: three same-prefab spawns 1-2 m apart each bind their
		// own record, and no record binds a sibling.
		var first = Key("turret", 0, 0, 2001, 1);
		var second = Key("turret", 1, 0, 2001, 2);
		var third = Key("turret", 2, 0, 2001, 3);
		var candidates = new List<RuntimeEntityMatch.Candidate>
		{
			Marked("turret", 0f, 0f, first),
			Marked("turret", 1.5f, 0f, second),
			Marked("turret", 2.5f, 0f, third),
		};

		Assert.Equal(0, RuntimeEntityMatch.FindIndex(candidates, first, 0f, 0f));
		Assert.Equal(1, RuntimeEntityMatch.FindIndex(candidates, second, 1.5f, 0f));
		Assert.Equal(2, RuntimeEntityMatch.FindIndex(candidates, third, 2.5f, 0f));
	}

	[Fact]
	public void FindIndex_NoCandidates_ReturnsMinusOne() =>
		Assert.Equal(-1, RuntimeEntityMatch.FindIndex([], Key("keypad", 0, 0, 2001, 1), 0f, 0f));
}
