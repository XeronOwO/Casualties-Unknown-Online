using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The runtime entity-creation match judgment: which local BuildingEntity a
/// creation record binds to. The 1 m radius is a bug fix (a 3 m radius absorbed
/// consecutive spawns of the same prefab — three turrets ~1-2 m apart, only the
/// first reached the peer), and the tutorial-prop exclusion keeps a shared
/// domain entity from binding to a per-player course object. Both rules are
/// pure here, so the adapter's Unity scan stays a thin candidate supplier.
/// </summary>
public class RuntimeEntityMatchTests
{
	private static RuntimeEntityMatch.Candidate Candidate(string id, float x, float y, bool tutorial = false, bool sameCreation = false) =>
		new(id, x, y, tutorial, sameCreation);

	[Fact]
	public void FindIndex_BindsTheSamePrefabInsideTheRadius()
	{
		var candidates = new List<RuntimeEntityMatch.Candidate> { Candidate("keypad", 0.5f, 0f) };

		Assert.Equal(0, RuntimeEntityMatch.FindIndex(candidates, "keypad", 0f, 0f));
	}

	[Fact]
	public void FindIndex_RejectsTheRadiusBoundary()
	{
		// Strictly inside: the boundary itself is NOT a match (the live relay's
		// same-prefab float noise stays well under it).
		Assert.Equal(0, RuntimeEntityMatch.FindIndex([Candidate("keypad", 0.999f, 0f)], "keypad", 0f, 0f));
		Assert.Equal(-1, RuntimeEntityMatch.FindIndex([Candidate("keypad", 1f, 0f)], "keypad", 0f, 0f));
	}

	[Fact]
	public void FindIndex_RejectsADifferentPrefab()
	{
		var candidates = new List<RuntimeEntityMatch.Candidate> { Candidate("landmine", 0.1f, 0f) };

		Assert.Equal(-1, RuntimeEntityMatch.FindIndex(candidates, "keypad", 0f, 0f));
	}

	[Fact]
	public void FindIndex_NeverBindsAPerPlayerTutorialProp()
	{
		// The shared-domain entity must not absorb another player's private
		// course object, even at the same prefab and position.
		var candidates = new List<RuntimeEntityMatch.Candidate> { Candidate("keypad", 0f, 0f, tutorial: true) };

		Assert.Equal(-1, RuntimeEntityMatch.FindIndex(candidates, "keypad", 0f, 0f));
	}

	[Fact]
	public void FindIndex_ThreeSpawnedTurretsCloseTogether_EachBindsItsOwnCopy()
	{
		// The regression that reduced the radius from 3 m: three same-prefab
		// spawns 1-2 m apart must not collapse onto the first copy.
		var candidates = new List<RuntimeEntityMatch.Candidate>
		{
			Candidate("turret", 0f, 0f),
			Candidate("turret", 1.5f, 0f),
			Candidate("turret", 2.5f, 0f),
		};

		Assert.Equal(0, RuntimeEntityMatch.FindIndex(candidates, "turret", 0f, 0f));
		Assert.Equal(1, RuntimeEntityMatch.FindIndex(candidates, "turret", 1.5f, 0f));
		Assert.Equal(2, RuntimeEntityMatch.FindIndex(candidates, "turret", 2.5f, 0f));
	}

	[Fact]
	public void FindIndex_PrefersTheSameCreationMarkerEvenWhenItDriftedFarAway()
	{
		// A runtime creation's Rigidbody2D becomes Dynamic while its chunk is
		// visible, so the copy can be far outside the radius when a re-report or
		// a re-broadcast arrives. The stamped creation key is the identity.
		var candidates = new List<RuntimeEntityMatch.Candidate>
		{
			Candidate("keypad", 40f, -12f, sameCreation: true),
			Candidate("keypad", 0.1f, 0f), // a DIFFERENT keypad near the recorded position
		};

		Assert.Equal(0, RuntimeEntityMatch.FindIndex(candidates, "keypad", 0f, 0f));
	}

	[Fact]
	public void FindIndex_NoCandidates_ReturnsMinusOne() =>
		Assert.Equal(-1, RuntimeEntityMatch.FindIndex([], "keypad", 0f, 0f));
}
