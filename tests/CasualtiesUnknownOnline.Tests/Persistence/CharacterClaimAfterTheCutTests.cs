using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The claim a peer makes for a stored character the restore could not bind (2026-11-24).
/// The restore binds once, against the peers present at the cut; a member who joined after
/// it owns nothing, and its bag comes back empty unless the archive it wrote can still be
/// claimed. Two rules decide it — the key's own claimant, then the transport handover by
/// display name — and the answer is only ever for the peer that asked.
/// </summary>
public class CharacterClaimAfterTheCutTests
{
	private static readonly PlayerIdentity Bob = new(9UL, "Bob");
	private static readonly PlayerIdentity Host = new(76561198000000001UL, "Host");

	[Fact]
	public void ClaimFor_ThePeerTheKeyNames_GetsItsOwnCharacter()
	{
		var stored = Stored("name-bob", "Bob");

		var claimed = WorldCharacterBinder.ClaimFor([stored], Bob.PeerId, [Host, Bob], PlayerKeySpace.IpDirect, []);

		Assert.Same(stored, claimed);
	}

	[Fact]
	public void ClaimFor_APeerTheKeyDoesNotName_GetsNothing()
	{
		var stored = Stored("name-bob", "Bob");

		var claimed = WorldCharacterBinder.ClaimFor([stored], Host.PeerId, [Host, Bob], PlayerKeySpace.IpDirect, []);

		Assert.Null(claimed);
	}

	[Fact]
	public void ClaimFor_TwoPeersSharingOneName_ClaimsNothingForEither()
	{
		// IP-direct allows duplicate display names by design; handing either one the file
		// could give a player somebody else's body, so the archive keeps it.
		var stored = Stored("name-bob", "Bob");
		var otherBob = new PlayerIdentity(11UL, "Bob");

		Assert.Null(WorldCharacterBinder.ClaimFor([stored], Bob.PeerId, [Host, Bob, otherBob], PlayerKeySpace.IpDirect, []));
		Assert.Null(WorldCharacterBinder.ClaimFor([stored], otherBob.PeerId, [Host, Bob, otherBob], PlayerKeySpace.IpDirect, []));
	}

	[Fact]
	public void ClaimFor_APeerThatAlreadyHasACharacter_IsNotHandedASecond()
	{
		// The handover half only fills a gap: a peer the cut already bound keeps its own file.
		var stored = Stored("steam-76561198000000002", "Bob");

		var claimed = WorldCharacterBinder.ClaimFor([stored], Bob.PeerId, [Host, Bob], PlayerKeySpace.Steam, new HashSet<ulong> { Bob.PeerId });

		Assert.Null(claimed);
	}

	private static SavedCharacter Stored(string key, string name) =>
		new(key, new CharacterDataMsg(), name);
}
