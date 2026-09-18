using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The pure host-side enemy combat arbitration (EnemyCombatArbitration): the
/// Game Adapter gathers candidate positions (host body + remote entity
/// stream), this machine makes the nearest-player and bite-gate decisions that
/// drive where the host's enemies aim and when a bite action fires. Whether an
/// attack CONNECTED is judged by the victim's own client
/// (EnemyAttackJudgmentTests). L0 coverage is the replacement for manual
/// dual-open acceptance of the decision layer; the Unity boundary stays behind
/// patch + field contracts.
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
	public void SelectBiteVictim_LocalVictimIsReturnedToo()
	{
		var selected = EnemyCombatArbitration.SelectBiteVictim(
			Candidates(), new NetVector2(0f, 0f), 1.5f, 0f, 0f);

		Assert.True(selected is { } fact && fact.SteamId == HostId,
			"the local body rides the native path, but the action is still announced so a guest in the same spot judges it");
	}

	[Fact]
	public void SelectBiteVictim_NoCandidateInRange_IsNull()
	{
		var selected = EnemyCombatArbitration.SelectBiteVictim(
			Candidates(), new NetVector2(10f, 0f), 1.5f, 0f, 0f);

		Assert.Null(selected);
	}
}
