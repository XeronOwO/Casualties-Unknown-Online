using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The transport-handover rule (2026-11-11): the same person keeps one character when the
/// transport changes. The match is by display name and only when it is unambiguous — a save
/// the game cannot name must not guess whose character it is.
/// </summary>
public class CharacterAdoptionTests
{
	private const ulong GuestId = 2002UL;
	private const ulong OtherId = 3003UL;

	[Fact]
	public void Adopts_TheOnlyCrossSpaceCharacterWithTheSameName()
	{
		var stored = new[] { Character("steam-1001", "Guest") };
		var peers = new[] { new PlayerIdentity(GuestId, "Guest") };

		var (adopted, ambiguous) = CharacterAdoption.Resolve(stored, peers, new List<ulong>());

		var pair = Assert.Single(adopted);
		Assert.Equal(GuestId, pair.PeerId);
		Assert.Equal("steam-1001", pair.Character.PlayerKey);
		Assert.Empty(ambiguous);
	}

	[Fact]
	public void TwoCharactersWithOneName_AdoptNothing()
	{
		var stored = new[] { Character("steam-1001", "Guest"), Character("name-guest", "guest") };
		var peers = new[] { new PlayerIdentity(GuestId, "Guest") };

		var (adopted, ambiguous) = CharacterAdoption.Resolve(stored, peers, new List<ulong>());

		Assert.Empty(adopted);
		Assert.Contains(GuestId, ambiguous);
	}

	[Fact]
	public void TwoPeersWithOneName_AdoptNothing()
	{
		var stored = new[] { Character("steam-1001", "Guest") };
		var peers = new[] { new PlayerIdentity(GuestId, "Guest"), new PlayerIdentity(OtherId, "guest") };

		var (adopted, ambiguous) = CharacterAdoption.Resolve(stored, peers, new List<ulong>());

		Assert.Empty(adopted);
		Assert.Empty(ambiguous);
	}

	[Fact]
	public void APeerThatAlreadyHasACharacter_AdoptsNothing()
	{
		var stored = new[] { Character("steam-1001", "Guest") };
		var peers = new[] { new PlayerIdentity(GuestId, "Guest") };

		var (adopted, _) = CharacterAdoption.Resolve(stored, peers, new List<ulong> { GuestId });

		Assert.Empty(adopted);
	}

	private static SavedCharacter Character(string key, string? displayName) =>
		new(key, new CharacterDataMsg(), displayName);
}
