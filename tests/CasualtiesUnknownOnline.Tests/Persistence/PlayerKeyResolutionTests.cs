using System;
using System.Globalization;
using CasualtiesUnknownOnline.Runtime.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The transport-scoped player key plumbing (§2, decision 162): what key a peer
/// writes under, and which present peer claims a stored key. The two key spaces
/// must never cross — a world written over Steam is not claimed by an IP-direct
/// name collision, and an unclaimed key means "that player is absent" (joins as a
/// new character), never an error.
/// </summary>
public class PlayerKeyResolutionTests
{
	private static readonly PlayerIdentity Host = new(76561198000000001UL, "Host");
	private static readonly PlayerIdentity NamedBob = new(9UL, "Bob");

	[Fact]
	public void KeyOf_SteamSpace_UsesTheAccountAndDefaultsToIt()
	{
		Assert.Equal("steam-76561198000000001", PlayerKeyResolution.KeyOf(Host.PeerId, Host.DisplayName, PlayerKeySpace.Steam));
		Assert.Equal("steam-76561198000000001", PlayerKeyResolution.KeyOf(Host.PeerId, Host.DisplayName, PlayerKeySpace.Unknown));
	}

	[Fact]
	public void KeyOf_IpDirectSpace_UsesTheSanitizedDisplayName()
	{
		Assert.Equal("name-bob", PlayerKeyResolution.KeyOf(NamedBob.PeerId, NamedBob.DisplayName, PlayerKeySpace.IpDirect));
		Assert.Equal("name-bob", PlayerKeyResolution.KeyOf(NamedBob.PeerId, "  BOB ", PlayerKeySpace.IpDirect));
	}

	[Fact]
	public void TrySpaceOfSet_DerivesTheSpaceFromTheKeys()
	{
		Assert.True(PlayerKeyResolution.TrySpaceOfSet(["steam-1", "steam-2"], out var steam));
		Assert.Equal(PlayerKeySpace.Steam, steam);

		Assert.True(PlayerKeyResolution.TrySpaceOfSet(["name-a", "name-b"], out var ipDirect));
		Assert.Equal(PlayerKeySpace.IpDirect, ipDirect);

		Assert.True(PlayerKeyResolution.TrySpaceOfSet([], out var empty));
		Assert.Equal(PlayerKeySpace.Unknown, empty);
	}

	[Fact]
	public void TrySpaceOfSet_RefusesAMixedOrForeignSet()
	{
		Assert.False(PlayerKeyResolution.TrySpaceOfSet(["steam-1", "name-bob"], out var mixed));
		Assert.Equal(PlayerKeySpace.Unknown, mixed);
		Assert.False(PlayerKeyResolution.TrySpaceOfSet(["something-else"], out _));

		// A bare prefix still derives the space (the prefix IS the grammar); it just
		// cannot claim anybody, because the account half does not parse.
		Assert.True(PlayerKeyResolution.TrySpaceOfSet(["steam-"], out var barePrefix));
		Assert.Equal(PlayerKeySpace.Steam, barePrefix);
		Assert.False(PlayerKeyResolution.TryResolve("steam-", PlayerKeySpace.Steam, [Host], out _));
	}

	[Fact]
	public void TryResolve_SteamSpace_ClaimsByAccountOnly()
	{
		Assert.True(PlayerKeyResolution.TryResolve("steam-76561198000000001", PlayerKeySpace.Steam, [Host, NamedBob], out var peerId));
		Assert.Equal(Host.PeerId, peerId);

		// Present but not that account: nobody claims it.
		Assert.False(PlayerKeyResolution.TryResolve("steam-76561198000000002", PlayerKeySpace.Steam, [Host, NamedBob], out _));
		Assert.False(PlayerKeyResolution.TryResolve("name-bob", PlayerKeySpace.Steam, [Host, NamedBob], out _));
	}

	[Fact]
	public void TryResolve_IpDirectSpace_ClaimsByNameRegardlessOfPunctuation()
	{
		Assert.True(PlayerKeyResolution.TryResolve("name-bob", PlayerKeySpace.IpDirect, [Host, NamedBob], out var peerId));
		Assert.Equal(NamedBob.PeerId, peerId);

		Assert.False(PlayerKeyResolution.TryResolve("steam-76561198000000001", PlayerKeySpace.IpDirect, [Host, NamedBob], out _));
		Assert.False(PlayerKeyResolution.TryResolve("name-carol", PlayerKeySpace.IpDirect, [Host, NamedBob], out _));
	}

	[Fact]
	public void KeyIn_UsesThePeerAndTheSpace()
	{
		Assert.Equal("steam-9", NamedBob.KeyIn(PlayerKeySpace.Steam));
		Assert.Equal("name-bob", NamedBob.KeyIn(PlayerKeySpace.IpDirect));
	}

	[Fact]
	public void TryResolve_NonLatinDisplayName_StillRoundTripsItsOwnKey()
	{
		// A name that loses its readable form still keys stably: the key carries a
		// digest, so two different non-Latin names never collapse (S1 contract).
		var playerKey = PlayerKeyResolution.KeyOf(7UL, "玩家甲", PlayerKeySpace.IpDirect);

		Assert.StartsWith("name-", playerKey, StringComparison.Ordinal);
		Assert.True(PlayerKeyResolution.TryResolve(playerKey, PlayerKeySpace.IpDirect, [new PlayerIdentity(7UL, "玩家甲")], out var peerId));
		Assert.Equal(7UL, peerId);
		Assert.False(PlayerKeyResolution.TryResolve(playerKey, PlayerKeySpace.IpDirect, [new PlayerIdentity(7UL, "玩家乙")], out _));
	}

	[Fact]
	public void SteamIdOf_OnlyAcceptsTheSteamGrammar()
	{
		Assert.Equal(76561198000000001UL, PlayerKeyResolution.SteamIdOf("steam-76561198000000001"));
		Assert.Null(PlayerKeyResolution.SteamIdOf("name-bob"));
		Assert.Null(PlayerKeyResolution.SteamIdOf("steam-bob"));
		Assert.Null(PlayerKeyResolution.SteamIdOf(string.Empty));
	}

	[Fact]
	public void KeyOf_UsesTheInvariantCulture()
	{
		Assert.Equal(
			"steam-" + ulong.MaxValue.ToString(CultureInfo.InvariantCulture),
			PlayerKeyResolution.KeyOf(ulong.MaxValue, null, PlayerKeySpace.Steam));
	}
}
