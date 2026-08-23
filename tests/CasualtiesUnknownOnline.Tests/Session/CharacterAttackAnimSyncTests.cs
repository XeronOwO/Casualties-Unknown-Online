using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The character attack-animation event (CharacterAttackAnimMsg): a player's
/// Body.Attack instantiates its one-shot attackAnim prefab locally and travels
/// as one dedicated reliable message — guest reports reach the host (whose
/// adapter replays it on the guest's clone) and relay to the other guests; the
/// host's own visual broadcasts to every guest. One animation = one message;
/// there is no snapshot fallback to assert (the visual has no persistent state).
/// </summary>
public class CharacterAttackAnimSyncTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;

	private static CharacterAttackAnimMsg ClawAnim(ulong owner = GuestId) => new()
	{
		OwnerSteamId = owner,
		Prefab = "ClawAnim",
		Position = new NetVector2Msg { X = 10f, Y = 20f },
		Direction = new NetVector2Msg { X = 1f, Y = 0f },
		IsRight = true,
	};

	[Fact]
	public void CharacterAttackAnim_RoundTripsEveryField()
	{
		var source = ClawAnim();

		var decoded = NetPacket.DecodePayload<CharacterAttackAnimMsg>(NetPacket.Encode(NetMsg.CharacterAttackAnim, source));

		Assert.Equal(source.OwnerSteamId, decoded.OwnerSteamId);
		Assert.Equal(source.Prefab, decoded.Prefab);
		Assert.Equal(source.Position.X, decoded.Position.X);
		Assert.Equal(source.Position.Y, decoded.Position.Y);
		Assert.Equal(source.Direction.X, decoded.Direction.X);
		Assert.Equal(source.Direction.Y, decoded.Direction.Y);
		Assert.Equal(source.IsRight, decoded.IsRight);
	}

	[Fact]
	public void GuestReport_HostFiresTheEvent_AndRelaysToTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var hostStore = w.Host.Services.GetRequiredService<CharacterDataStore>();
		var applied = 0;
		hostStore.CharacterAttackAnimReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<CharacterDataStore>().SendCharacterAttackAnim(ClawAnim(w.G1.SteamId));
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the host must fire the received event (the adapter replays it)");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.CharacterAttackAnim) == 1, "the host must relay the animation to the other guest");
	}

	[Fact]
	public void HostOwnAnimation_BroadcastsToBothGuests()
	{
		using var w = ItemSimWorld.Create();

		w.Host.Services.GetRequiredService<CharacterDataStore>().SendCharacterAttackAnim(ClawAnim(HostId));
		w.Driver.Tick(33);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.CharacterAttackAnim) == 1, "the host's own animation must reach G1");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.CharacterAttackAnim) == 1, "the host's own animation must reach G2");
	}

	[Fact]
	public void GuestRelay_FiresTheEventOnTheOtherGuest()
	{
		using var w = ItemSimWorld.Create();
		var g2Store = w.G2.Services.GetRequiredService<CharacterDataStore>();
		var applied = 0;
		g2Store.CharacterAttackAnimReceived += (_, _) => applied++;

		w.G1.Services.GetRequiredService<CharacterDataStore>().SendCharacterAttackAnim(ClawAnim(w.G1.SteamId));
		w.Driver.Tick(33);

		Assert.True(applied == 1, "the relayed animation must fire the received event on the other guest");
	}
}
