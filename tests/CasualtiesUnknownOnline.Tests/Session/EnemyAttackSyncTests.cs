using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The host-ordered enemy attack chain (EnemyAttackMsg): the host's enemy
/// simulation targets a remote player (whose render clone has no colliders),
/// so the host sends the one-shot command; the victim's Game Adapter applies
/// the attack locally and reports the post-attack terminal state through the
/// kernel combat-result events.
/// </summary>
public class EnemyAttackSyncTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static EnemyAttackMsg Attack(EnemyAttackKind kind = EnemyAttackKind.SpiderBite, int limbIndex = 0) => new()
	{
		EnemyId = new NetworkEntityIdMsg(7, 3, 0),
		VictimSteamId = GuestId,
		Kind = kind,
		LimbIndex = limbIndex,
	};

	[Fact]
	public void EnemyAttack_RoundTripsTheCommand()
	{
		var source = Attack();

		var decoded = NetPacket.DecodePayload<EnemyAttackMsg>(
			NetPacket.Encode(NetMsg.EnemyAttack, source));

		Assert.Equal(source.EnemyId.ToNetworkEntityId(), decoded.EnemyId.ToNetworkEntityId());
		Assert.Equal(source.VictimSteamId, decoded.VictimSteamId);
		Assert.Equal(source.Kind, decoded.Kind);
		Assert.Equal(source.LimbIndex, decoded.LimbIndex);
	}

	[Fact]
	public void EnemyAttack_LimbMinusOne_RoundTripsExplicitly()
	{
		var decoded = NetPacket.DecodePayload<EnemyAttackMsg>(
			NetPacket.Encode(NetMsg.EnemyAttack, Attack(limbIndex: -1)));

		Assert.Equal(-1, decoded.LimbIndex);
	}

	[Fact]
	public void HostCommand_ArrivesOnlyAtTheVictimGuest()
	{
		using var w = ItemSimWorld.Create();
		var hostEnemies = w.Host.Services.GetRequiredService<EnemySyncService>();
		foreach (var member in w.Host.Session.Members)
		{
			member.InWorld = true;
		}

		hostEnemies.SendEnemyAttack(Attack());

		w.Driver.Tick(33);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.EnemyAttack) == 1, "the victim guest must receive the command");
		Assert.True(w.ReceivedCount(w.G2, NetMsg.EnemyAttack) == 0, "a non-victim guest must not receive another player's attack command");
	}

	[Fact]
	public void HostCommand_FiresTheApplyEventOnTheVictimGuest()
	{
		using var w = ItemSimWorld.Create();
		var hostEnemies = w.Host.Services.GetRequiredService<EnemySyncService>();
		var victimEnemies = w.G1.Services.GetRequiredService<EnemySyncService>();
		foreach (var member in w.Host.Session.Members)
		{
			member.InWorld = true;
		}

		var applied = 0;
		victimEnemies.EnemyAttackReceived += msg =>
		{
			if (msg.Kind == EnemyAttackKind.SpiderBite && msg.VictimSteamId == w.G1.SteamId)
			{
				applied++;
			}
		};

		hostEnemies.SendEnemyAttack(Attack());

		w.Driver.Tick(33);

		Assert.True(applied == 1, "the command must reach the victim's Game Adapter seam (EnemyAttackReceived)");
	}

	[Fact]
	public void HostCommand_IsDroppedWhenTheVictimIsNotInWorld()
	{
		using var w = ItemSimWorld.Create();
		var hostEnemies = w.Host.Services.GetRequiredService<EnemySyncService>();

		hostEnemies.SendEnemyAttack(Attack());

		w.Driver.Tick(33);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.EnemyAttack) == 0, "a menu/loading victim cannot receive an in-world attack");
	}
}
