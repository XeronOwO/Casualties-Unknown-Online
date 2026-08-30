using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The pure host-side enemy combat arbitration (EnemyCombatArbitration): the
/// Game Adapter gathers candidate positions (host body + remote entity
/// stream), this machine makes the nearest-player / lunge-ray / bite-gate
/// decisions. L0 coverage is the replacement for manual dual-open acceptance
/// of the decision layer; the Unity boundary stays behind patch + field
/// contracts.
/// </summary>
public class EnemyCombatArbitrationTests
{
	private const ulong HostId = 1001;
	private const ulong GuestA = 2001;
	private const ulong GuestB = 3001;

	private static EnemyTargetFact Target(ulong steamId, float x, float y) => new(steamId, new NetVector2(x, y));

	private static List<EnemyTargetFact> Candidates() =>
	[
		Target(HostId, 0f, 0f),
		Target(GuestA, 3f, 0f),
		Target(GuestB, 6f, 0f),
	];

	[Fact]
	public void SelectNearest_PicksTheClosestCandidateWithinRange()
	{
		var selected = EnemyCombatArbitration.SelectNearest(Candidates(), new NetVector2(0f, 0f), 8f);

		Assert.True(selected is { } fact && fact.SteamId == HostId, "the host body at the origin is nearest");
	}

	[Fact]
	public void SelectNearest_ReturnsNullWhenEveryoneIsOutOfRange()
	{
		var selected = EnemyCombatArbitration.SelectNearest(Candidates(), new NetVector2(20f, 0f), 8f);

		Assert.Null(selected);
	}

	[Fact]
	public void SelectNearest_TiesKeepTheInputOrder()
	{
		var candidates = new List<EnemyTargetFact>
		{
			Target(GuestA, 3f, 0f),
			Target(GuestB, -3f, 0f),
		};

		var selected = EnemyCombatArbitration.SelectNearest(candidates, new NetVector2(0f, 0f), 8f);

		Assert.True(selected is { } fact && fact.SteamId == GuestA, "ties must be deterministic (first in the input order)");
	}

	[Fact]
	public void SelectBiteVictim_ReturnsTheNearestRemotePlayer()
	{
		var selected = EnemyCombatArbitration.SelectBiteVictim(
			Candidates(), new NetVector2(2.5f, 0f), biteRange: 1.5f, biteCooldown: 0f, stunTime: 0f);

		Assert.True(selected is { } fact && fact.SteamId == GuestA, "GuestA is the only player inside the 1.5-unit bite radius");
	}

	[Fact]
	public void SelectBiteVictim_ClosedByCooldownOrStun()
	{
		Assert.Null(EnemyCombatArbitration.SelectBiteVictim(
			Candidates(), new NetVector2(2.5f, 0f), 1.5f, biteCooldown: 0.01f, stunTime: 0f));
		Assert.Null(EnemyCombatArbitration.SelectBiteVictim(
			Candidates(), new NetVector2(2.5f, 0f), 1.5f, biteCooldown: 0f, stunTime: 0.01f));
	}

	[Fact]
	public void SelectBiteVictim_LocalVictimIsReturnedForTheOrderPolicy()
	{
		var selected = EnemyCombatArbitration.SelectBiteVictim(
			Candidates(), new NetVector2(0f, 0f), 1.5f, 0f, 0f);

		Assert.True(selected is { } fact && fact.SteamId == HostId,
			"arbitration selects the nearest player; EnemyCombatOrderPolicy decides the local-native path");
	}

	[Fact]
	public void SelectBiteVictim_NoCandidateInRange_IsNull()
	{
		var selected = EnemyCombatArbitration.SelectBiteVictim(
			Candidates(), new NetVector2(10f, 0f), 1.5f, 0f, 0f);

		Assert.Null(selected);
	}

	[Fact]
	public void SelectLungeVictim_FirstAlongTheRayBeforeGroundWins()
	{
		var candidates = new List<EnemyTargetFact>
		{
			Target(HostId, 1f, 0.05f),
			Target(GuestA, 3f, 0f),
			Target(GuestB, 9f, 0f),
		};
		var origin = new NetVector2(0f, 0f);
		var direction = new NetVector2(1f, 0f);

		var selected = EnemyCombatArbitration.SelectLungeVictim(candidates, origin, direction, groundDistance: 999f, rayTolerance: 0.5f);

		Assert.True(selected is { } fact && fact.SteamId == HostId, "the local body is first along the ray — the native game raycast handles it");
	}

	[Fact]
	public void SelectLungeVictim_GroundStopsTheRay()
	{
		var candidates = new List<EnemyTargetFact>
		{
			Target(HostId, 3f, 0f),
			Target(GuestA, 6f, 0f),
		};

		var selected = EnemyCombatArbitration.SelectLungeVictim(
			candidates, new NetVector2(0f, 0f), new NetVector2(1f, 0f), groundDistance: 4f, rayTolerance: 0.5f);

		Assert.True(selected is { } fact && fact.SteamId == HostId, "GuestA is behind the ground hit and cannot be lunge-hit");
	}

	[Fact]
	public void SelectLungeVictim_OffRayOrBehindOrigin_IsIgnored()
	{
		var candidates = new List<EnemyTargetFact>
		{
			Target(GuestA, -3f, 0f), // behind the origin
			Target(GuestB, 3f, 5f),  // 5 units off the ray
		};

		var selected = EnemyCombatArbitration.SelectLungeVictim(
			candidates, new NetVector2(0f, 0f), new NetVector2(1f, 0f), groundDistance: 999f, rayTolerance: 0.5f);

		Assert.Null(selected);
	}
}
