using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class CharacterReconnectAdoptionTests
{
	private const ulong NewPeer = 3001;
	private const ulong OldPeer = 2001;

	private static ReconnectCandidate Candidate(ulong peer, string name, bool inWorld = false, bool handshaken = true) =>
		new(peer, name, handshaken, inWorld);

	[Fact]
	public void OneOldSameNameOutOfWorld_IsTheReconnect()
	{
		var source = CharacterReconnectAdoption.FindSource(
			NewPeer,
			"Guest",
			[Candidate(OldPeer, "Guest")],
			new HashSet<ulong> { OldPeer });

		Assert.Equal(OldPeer, source);
	}

	[Fact]
	public void OldOwnerStillInWorld_IsNotAReconnect()
	{
		var source = CharacterReconnectAdoption.FindSource(
			NewPeer,
			"Guest",
			[Candidate(OldPeer, "Guest", inWorld: true)],
			new HashSet<ulong> { OldPeer });

		Assert.Null(source);
	}

	[Fact]
	public void TwoOldSameNamePresences_AreAmbiguous()
	{
		var source = CharacterReconnectAdoption.FindSource(
			NewPeer,
			"Guest",
			[Candidate(OldPeer, "Guest"), Candidate(4001, "guest")],
			new HashSet<ulong> { OldPeer, 4001 });

		Assert.Null(source);
	}

	[Fact]
	public void OldPresenceWithoutASavedCharacter_IsNotAdopted()
	{
		var source = CharacterReconnectAdoption.FindSource(
			NewPeer,
			"Guest",
			[Candidate(OldPeer, "guest")],
			new HashSet<ulong>());

		Assert.Null(source);
	}

	[Fact]
	public void EmptyDisplayName_NeverMatches()
	{
		var source = CharacterReconnectAdoption.FindSource(
			NewPeer,
			"  ",
			[Candidate(OldPeer, "Guest")],
			new HashSet<ulong> { OldPeer });

		Assert.Null(source);
	}

	[Fact]
	public void OldPresenceNotHandshaken_IsNotAdopted()
	{
		var source = CharacterReconnectAdoption.FindSource(
			NewPeer,
			"Guest",
			[Candidate(OldPeer, "Guest", handshaken: false)],
			new HashSet<ulong> { OldPeer });

		Assert.Null(source);
	}

	[Fact]
	public void NewPresenceAlreadyReportsTheSameName_DoesNotShadowItsOwnOldRow()
	{
		var source = CharacterReconnectAdoption.FindSource(
			NewPeer,
			"Guest",
			[Candidate(NewPeer, "Guest")],
			new HashSet<ulong> { OldPeer },
			new Dictionary<ulong, string> { { OldPeer, "Guest" }, { NewPeer, "Guest" } });

		Assert.Equal(OldPeer, source);
	}
}
